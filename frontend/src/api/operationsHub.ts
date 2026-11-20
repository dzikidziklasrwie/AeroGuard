import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { ChatMessageRecord, FlightRecord } from './types';

export interface OperationsHubHandlers {
  onFlightUpdated: (flight: FlightRecord) => void;
  onFlightRemoved: (flightId: number, eventSequence?: number) => void;
  onMessagePosted: (message: ChatMessageRecord) => void;
  onReconnected: () => void | Promise<void>;
}

export async function connectOperationsHub(
  token: string,
  handlers: OperationsHubHandlers,
): Promise<() => Promise<void>> {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/operations', { accessTokenFactory: () => token })
    .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
    .configureLogging(LogLevel.None)
    .build();
  const flightRemoved = (payload: number | { id: number; event_sequence?: number }) => {
    handlers.onFlightRemoved(
      typeof payload === 'number' ? payload : payload.id,
      typeof payload === 'number' ? undefined : payload.event_sequence,
    );
  };

  connection.on('flight_updated', handlers.onFlightUpdated);
  connection.on('flight_removed', flightRemoved);
  connection.on('message_posted', handlers.onMessagePosted);
  connection.onreconnected(() => {
    void handlers.onReconnected();
  });
  await connection.start();

  return async () => {
    connection.off('flight_updated', handlers.onFlightUpdated);
    connection.off('flight_removed', flightRemoved);
    connection.off('message_posted', handlers.onMessagePosted);
    await connection.stop();
  };
}
