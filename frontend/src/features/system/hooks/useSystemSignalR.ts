import { useEffect } from 'react';
import * as signalR from '@microsoft/signalr';
import { useSystemStore } from '@/store/systemStore';

export function useSystemSignalR() {
  const { setUpdateProgress, setUpdateComplete } = useSystemStore();

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('http://127.0.0.1:5000/hubs/system')
      .withAutomaticReconnect()
      .build();

    connection.on('UpdateProgress', (progress: number) => {
      setUpdateProgress(progress);
    });

    connection.on('UpdateComplete', () => {
      setUpdateProgress(100);
      setUpdateComplete(true);
    });

    const startConnection = async () => {
      try {
        await connection.start();
        console.log('SystemHub connected');
      } catch (err) {
        console.error('SystemHub connection failed: ', err);
      }
    };

    startConnection();

    return () => {
      connection.stop();
    };
  }, [setUpdateProgress, setUpdateComplete]);
}
