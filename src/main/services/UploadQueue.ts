import fs from 'fs';
import path from 'path';
import { app } from 'electron';
import crypto from 'crypto';
import { QueuedSessionUpload, UploadSessionParams, MediaUploadItem } from '../../shared/types';

export class UploadQueueService {
  private queueFilePath: string;
  private queue: QueuedSessionUpload[] = [];

  constructor() {
    const userDataPath = app.getPath('userData');
    this.queueFilePath = path.join(userDataPath, 'upload_queue.json');
    this.loadQueue();
  }

  private loadQueue() {
    try {
      if (fs.existsSync(this.queueFilePath)) {
        const data = fs.readFileSync(this.queueFilePath, 'utf-8');
        const parsed = JSON.parse(data) || [];
        // Migration check if old schema (array of QueuedUpload without session level) was present
        if (Array.isArray(parsed) && parsed.length > 0 && 'bucketName' in parsed[0] && !('sessionId' in parsed[0])) {
          console.log('[UploadQueue] Migrating legacy upload queue format...');
          this.queue = [];
        } else {
          this.queue = parsed;
        }
      } else {
        this.queue = [];
      }
    } catch (e) {
      console.error('[UploadQueue] Failed to load offline queue:', e);
      this.queue = [];
    }
  }

  private saveQueue() {
    try {
      fs.writeFileSync(this.queueFilePath, JSON.stringify(this.queue, null, 2));
    } catch (e) {
      console.error('[UploadQueue] Failed to save offline queue:', e);
    }
  }

  public enqueueSession(params: UploadSessionParams): QueuedSessionUpload {
    const sessionsDir = path.join(app.getPath('documents'), 'Sebooth', 'OfflineCache');
    if (!fs.existsSync(sessionsDir)) {
      fs.mkdirSync(sessionsDir, { recursive: true });
    }

    const processedMediaItems: MediaUploadItem[] = params.mediaItems.map((item, index) => {
      let finalFilePath = item.filePath || '';

      // Save base64 data to offline cache file if filePath is missing
      if (item.base64Data && !finalFilePath) {
        const fileExt = item.mimeType.includes('png') ? 'png' : item.mimeType.includes('gif') ? 'gif' : 'jpg';
        finalFilePath = path.join(sessionsDir, `${params.sessionId}_${item.type}_${index}.${fileExt}`);
        const pureBase64 = item.base64Data.replace(/^data:([A-Za-z-+/]+);base64,/, '');
        fs.writeFileSync(finalFilePath, pureBase64, 'base64');
      }

      return {
        id: item.id || crypto.randomUUID(),
        type: item.type,
        bucketName: item.bucketName || params.bucketName || 'sebooth-media-konser',
        destinationPath: item.destinationPath,
        filePath: finalFilePath,
        mimeType: item.mimeType,
        label: item.label || item.type,
        metadata: item.metadata || {},
        status: item.status || 'pending',
        retries: item.retries || 0
      };
    });

    const existingIdx = this.queue.findIndex(q => q.sessionId === params.sessionId);
    let sessionEntry: QueuedSessionUpload;

    if (existingIdx !== -1) {
      sessionEntry = this.queue[existingIdx];
      processedMediaItems.forEach(newItem => {
        const itemIdx = sessionEntry.mediaItems.findIndex(m => m.destinationPath === newItem.destinationPath);
        if (itemIdx !== -1) {
          sessionEntry.mediaItems[itemIdx] = { ...sessionEntry.mediaItems[itemIdx], ...newItem };
        } else {
          sessionEntry.mediaItems.push(newItem);
        }
      });
    } else {
      sessionEntry = {
        sessionId: params.sessionId,
        eventName: params.eventName || 'Sebooth Event',
        createdAt: new Date().toISOString(),
        addedAt: Date.now(),
        sessionDbSynced: false,
        mediaItems: processedMediaItems
      };
      this.queue.push(sessionEntry);
    }

    this.saveQueue();
    console.log(`[UploadQueue] Enqueued session ${params.sessionId} (${processedMediaItems.length} items). Total pending sessions: ${this.queue.length}`);
    return sessionEntry;
  }

  public getPendingQueue(): QueuedSessionUpload[] {
    return [...this.queue];
  }

  public markSessionDbSynced(sessionId: string): void {
    const session = this.queue.find(q => q.sessionId === sessionId);
    if (session) {
      session.sessionDbSynced = true;
      this.saveQueue();
      this.removeSessionIfCompleted(sessionId);
    }
  }

  public markMediaItemGcsUploaded(sessionId: string, itemId: string): void {
    const session = this.queue.find(q => q.sessionId === sessionId);
    if (session) {
      const item = session.mediaItems.find(m => m.id === itemId);
      if (item) {
        item.status = 'gcs_uploaded';
        this.saveQueue();
      }
    }
  }

  public markMediaItemCompleted(sessionId: string, itemId: string): void {
    const session = this.queue.find(q => q.sessionId === sessionId);
    if (session) {
      const item = session.mediaItems.find(m => m.id === itemId);
      if (item) {
        item.status = 'completed';
        this.saveQueue();
        this.removeSessionIfCompleted(sessionId);
      }
    }
  }

  public incrementMediaItemRetry(sessionId: string, itemId: string): void {
    const session = this.queue.find(q => q.sessionId === sessionId);
    if (session) {
      const item = session.mediaItems.find(m => m.id === itemId);
      if (item) {
        item.retries = (item.retries || 0) + 1;
        this.saveQueue();
      }
    }
  }

  public removeSessionIfCompleted(sessionId: string): boolean {
    const idx = this.queue.findIndex(q => q.sessionId === sessionId);
    if (idx === -1) return false;

    const session = this.queue[idx];
    const allItemsDone = session.mediaItems.every(m => m.status === 'completed');

    if (session.sessionDbSynced && allItemsDone) {
      this.queue.splice(idx, 1);
      this.saveQueue();

      session.mediaItems.forEach(item => {
        if (item.filePath && item.filePath.includes('OfflineCache') && fs.existsSync(item.filePath)) {
          try {
            fs.unlinkSync(item.filePath);
          } catch (err) {
            console.warn(`[UploadQueue] Failed to delete temp file ${item.filePath}:`, err);
          }
        }
      });
      console.log(`[UploadQueue] Session ${sessionId} fully completed and removed from queue.`);
      return true;
    }
    return false;
  }
}

export const uploadQueue = new UploadQueueService();
