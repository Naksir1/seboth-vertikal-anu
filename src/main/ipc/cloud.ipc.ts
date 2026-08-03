import { ipcMain } from 'electron';
import { Storage } from '@google-cloud/storage';
import path from 'path';
import fs from 'fs';
import { config } from 'dotenv';
import { uploadQueue } from '../services/UploadQueue';
import { createClient, SupabaseClient } from '@supabase/supabase-js';
import { UploadSessionParams } from '../../shared/types';

// Load environment variables from .env
config();

// GCS Key Configuration
const gcsKeyName = process.env.GCS_KEY_PATH || 'google-key.json';
const keyPath = path.isAbsolute(gcsKeyName) 
  ? gcsKeyName 
  : path.join(process.cwd(), gcsKeyName);

let storage: Storage | null = null;

try {
  if (fs.existsSync(keyPath)) {
    storage = new Storage({ keyFilename: keyPath });
    console.log(`[Cloud Storage] Initialized successfully with: ${path.basename(keyPath)}`);
  } else {
    console.warn(`[Cloud Storage] Missing Google Cloud key at: ${keyPath}`);
    console.warn('[Cloud Storage] Please set GCS_KEY_PATH in .env or place google-key.json in the root.');
  }
} catch (error) {
  console.error('[Cloud Storage] Failed to initialize GCS:', error);
}

// Supabase Client Initialization for Main Process
const supabaseUrl = process.env.SUPABASE_URL || 'https://hfheuhivhwooaobgjtqv.supabase.co';
const supabaseAnonKey = process.env.SUPABASE_ANON_KEY || 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImhmaGV1aGl2aHdvb2FvYmdqdHF2Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzM3MDM3MDMsImV4cCI6MjA4OTI3OTcwM30.rN_R3eyhYPZmpOa7r921Eoo5rAJMxlgcWkv0vwQZohc';

let supabaseClient: SupabaseClient | null = null;
try {
  supabaseClient = createClient(supabaseUrl, supabaseAnonKey);
} catch (e) {
  console.error('[Cloud IPC] Failed to initialize Supabase client:', e);
}

// Worker to process offline queue
async function processOfflineQueue() {
  if (!storage || !supabaseClient) return;
  const pendingSessions = uploadQueue.getPendingQueue();
  if (pendingSessions.length === 0) return;

  console.log(`[Cloud Storage] Processing offline queue (${pendingSessions.length} pending sessions)...`);

  for (const session of pendingSessions) {
    try {
      // 1. Sync Sessions table in Supabase
      if (!session.sessionDbSynced) {
        const { error: dbErr } = await supabaseClient
          .from('sessions')
          .upsert({
            id: session.sessionId,
            event_name: session.eventName || 'Sebooth Event',
            is_claimed: false,
            created_at: session.createdAt || new Date().toISOString()
          }, { onConflict: 'id' });

        if (dbErr) {
          console.warn(`[Cloud Storage] Supabase DB session sync failed for ${session.sessionId}:`, dbErr.message);
          continue; // Wait until session DB upsert succeeds before uploading media rows
        } else {
          uploadQueue.markSessionDbSynced(session.sessionId);
          console.log(`[Cloud Storage] Supabase session DB row synced for ${session.sessionId}`);
        }
      }

      // 2. Process media items
      for (const item of session.mediaItems) {
        if (item.status === 'completed') continue;

        const bucketName = item.bucketName || 'sebooth-media-konser';
        const bucket = storage.bucket(bucketName);
        const publicUrl = `https://storage.googleapis.com/${bucketName}/${item.destinationPath}`;

        // 2a. GCS Upload if not yet done
        if (item.status !== 'gcs_uploaded') {
          if (!fs.existsSync(item.filePath!)) {
            console.warn(`[Cloud Storage] Queue item file missing: ${item.filePath}`);
            uploadQueue.markMediaItemCompleted(session.sessionId, item.id!);
            continue;
          }

          try {
            await bucket.upload(item.filePath!, {
              destination: item.destinationPath,
              metadata: {
                contentType: item.mimeType,
                cacheControl: 'public, max-age=31536000'
              }
            });
            uploadQueue.markMediaItemGcsUploaded(session.sessionId, item.id!);
            console.log(`[Cloud Storage] GCS Upload successful for ${item.destinationPath}`);
          } catch (gcsErr: any) {
            console.error(`[Cloud Storage] GCS Upload failed for ${item.destinationPath}:`, gcsErr.message);
            uploadQueue.incrementMediaItemRetry(session.sessionId, item.id!);
            continue; // Retry next interval
          }
        }

        // 2b. Supabase Media Row Insert
        try {
          const { error: mediaDbErr } = await supabaseClient
            .from('media')
            .insert({
              session_id: session.sessionId,
              type: item.type,
              url: publicUrl,
              metadata: item.destinationPath.includes('strip.jpg') ? { is_strip: true } : (item.metadata || {})
            });

          if (mediaDbErr) {
            console.error(`[Cloud Storage] Supabase Media Insert failed for ${item.destinationPath}:`, mediaDbErr.message);
            uploadQueue.incrementMediaItemRetry(session.sessionId, item.id!);
          } else {
            uploadQueue.markMediaItemCompleted(session.sessionId, item.id!);
            console.log(`[Cloud Storage] Supabase media record inserted for ${item.destinationPath}`);
          }
        } catch (dbInsertErr: any) {
          console.error(`[Cloud Storage] Media DB insert exception for ${item.destinationPath}:`, dbInsertErr.message);
        }
      }
    } catch (sessionErr: any) {
      console.error(`[Cloud Storage] Exception while processing queued session ${session.sessionId}:`, sessionErr);
    }
  }
}

// Background timer to check offline queue every 10 seconds
setInterval(processOfflineQueue, 10000);

export function registerCloudHandlers(): void {
  // Legacy single file upload
  ipcMain.handle('cloud:upload-file', async (_, params: {
    bucketName: string;
    destinationPath: string; // e.g. sessionId/photo.png
    filePath?: string;
    base64Data?: string;
    mimeType: string;
  }) => {
    if (!storage) {
       return { success: false, error: 'Google Cloud API Key not found' };
    }
    
    const publicUrl = `https://storage.googleapis.com/${params.bucketName}/${params.destinationPath}`;

    try {
      const bucket = storage.bucket(params.bucketName);
      const file = bucket.file(params.destinationPath);

      if (params.filePath) {
        if (!fs.existsSync(params.filePath)) {
           return { success: false, error: `File not found locally: ${params.filePath}` };
        }
        await bucket.upload(params.filePath, {
          destination: params.destinationPath,
          metadata: { 
              contentType: params.mimeType,
              cacheControl: 'public, max-age=31536000'
          }
        });
      } else if (params.base64Data) {
        const base64Content = params.base64Data.replace(/^data:([A-Za-z-+/]+);base64,/, '');
        const buffer = Buffer.from(base64Content, 'base64');
        await file.save(buffer, {
          metadata: { 
              contentType: params.mimeType,
              cacheControl: 'public, max-age=31536000'
          },
          resumable: false
        });
      } else {
        return { success: false, error: 'No data provided for upload' };
      }

      return { success: true, url: publicUrl, status: 'uploaded' };

    } catch (error: any) {
      console.warn('[Cloud Storage] Single upload failed:', error.message);
      return { success: false, error: error.message };
    }
  });

  // Main unified session upload IPC handler
  ipcMain.handle('cloud:upload-session', async (_, params: UploadSessionParams) => {
    const sessionId = params.sessionId;
    console.log(`[Cloud Storage] Received upload-session request for ${sessionId} (${params.mediaItems.length} items)...`);

    try {
      // 1. Enqueue to durable upload queue file
      const sessionQueue = uploadQueue.enqueueSession(params);

      if (!storage) {
        return {
          success: true,
          data: {
            sessionId,
            status: 'queued',
            message: 'Tersimpan Offline (GCS Key tidak terpasang, tersimpan di antrean).'
          }
        };
      }

      // 2. Try immediate Supabase Session Upsert
      let dbSynced = false;
      if (supabaseClient) {
        try {
          const { error: dbErr } = await supabaseClient
            .from('sessions')
            .upsert({
              id: sessionId,
              event_name: params.eventName || 'Sebooth Event',
              is_claimed: false,
              created_at: new Date().toISOString()
            }, { onConflict: 'id' });

          if (!dbErr) {
            dbSynced = true;
            uploadQueue.markSessionDbSynced(sessionId);
            console.log(`[Cloud Storage] Immediate DB upsert succeeded for session ${sessionId}`);
          } else {
            console.warn(`[Cloud Storage] Immediate DB upsert failed: ${dbErr.message}`);
          }
        } catch (e: any) {
          console.warn(`[Cloud Storage] DB connection warning: ${e.message}`);
        }
      }

      // 3. Try immediate upload for each media item
      let successCount = 0;
      const bucketName = params.bucketName || 'sebooth-media-konser';
      const bucket = storage.bucket(bucketName);

      for (const item of sessionQueue.mediaItems) {
        if (item.status === 'completed') {
          successCount++;
          continue;
        }

        const publicUrl = `https://storage.googleapis.com/${bucketName}/${item.destinationPath}`;

        try {
          // GCS Upload
          if (item.status !== 'gcs_uploaded') {
            if (fs.existsSync(item.filePath!)) {
              await bucket.upload(item.filePath!, {
                destination: item.destinationPath,
                metadata: {
                  contentType: item.mimeType,
                  cacheControl: 'public, max-age=31536000'
                }
              });
              uploadQueue.markMediaItemGcsUploaded(sessionId, item.id!);
            }
          }

          // Supabase Media Record Insert
          if (dbSynced && supabaseClient) {
            const { error: insErr } = await supabaseClient
              .from('media')
              .insert({
                session_id: sessionId,
                type: item.type,
                url: publicUrl,
                metadata: item.destinationPath.includes('strip.jpg') ? { is_strip: true } : (item.metadata || {})
              });

            if (!insErr) {
              uploadQueue.markMediaItemCompleted(sessionId, item.id!);
              successCount++;
            }
          }
        } catch (uploadErr: any) {
          console.warn(`[Cloud Storage] Immediate item upload deferred to offline queue for ${item.destinationPath}:`, uploadErr.message);
        }
      }

      const isFullyCompleted = uploadQueue.removeSessionIfCompleted(sessionId);

      if (isFullyCompleted) {
        return {
          success: true,
          data: {
            sessionId,
            status: 'uploaded',
            message: `Upload Selesai (${successCount}/${params.mediaItems.length} sukses)`
          }
        };
      } else {
        return {
          success: true,
          data: {
            sessionId,
            status: 'queued',
            message: 'Tersimpan Offline (Otomatis Sinkron saat online)'
          }
        };
      }

    } catch (err: any) {
      console.error(`[Cloud Storage] Session upload error for ${sessionId}:`, err);
      uploadQueue.enqueueSession(params);
      return {
        success: true,
        data: {
          sessionId,
          status: 'queued',
          message: 'Tersimpan Offline (Terjadi kesalahan jaringan, disimpan di antrean)'
        }
      };
    }
  });

  ipcMain.handle('cloud:get-queue', () => {
    return { success: true, data: uploadQueue.getPendingQueue() };
  });

  ipcMain.handle('cloud:sync-now', async () => {
    await processOfflineQueue();
    const remaining = uploadQueue.getPendingQueue().length;
    return { success: true, data: { remaining } };
  });
}
