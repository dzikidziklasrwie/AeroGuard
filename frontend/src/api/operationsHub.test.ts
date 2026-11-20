import { describe, expect, it, vi } from 'vitest';

const signalR = vi.hoisted(() => {
  const handlers = new Map<string, (...args: never[]) => void>();
  const connection = {
    on: vi.fn((name: string, handler: (...args: never[]) => void) => handlers.set(name, handler)),
    off: vi.fn(),
    onreconnected: vi.fn(),
    start: vi.fn().mockResolvedValue(undefined),
    stop: vi.fn().mockResolvedValue(undefined),
  };
  const builder = {
    withUrl: vi.fn(),
    withAutomaticReconnect: vi.fn(),
    configureLogging: vi.fn(),
    build: vi.fn(() => connection),
  };
  builder.withUrl.mockReturnValue(builder);
  builder.withAutomaticReconnect.mockReturnValue(builder);
  builder.configureLogging.mockReturnValue(builder);
  return { builder, connection, handlers };
});

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl = signalR.builder.withUrl;
    withAutomaticReconnect = signalR.builder.withAutomaticReconnect;
    configureLogging = signalR.builder.configureLogging;
    build = signalR.builder.build;
  },
  LogLevel: { None: 6 },
}));

import { connectOperationsHub } from './operationsHub';

describe('operations SignalR client', () => {
  it('uses the in-memory Bearer token and registers resynchronization after reconnect', async () => {
    const handlers = {
      onFlightUpdated: vi.fn(),
      onFlightRemoved: vi.fn(),
      onMessagePosted: vi.fn(),
      onReconnected: vi.fn(),
    };

    const disconnect = await connectOperationsHub('opaque-token', handlers);

    expect(signalR.builder.withUrl).toHaveBeenCalledWith('/hubs/operations', expect.objectContaining({
      accessTokenFactory: expect.any(Function),
    }));
    const options = signalR.builder.withUrl.mock.calls[0][1];
    expect(options.accessTokenFactory()).toBe('opaque-token');
    expect(signalR.connection.on).toHaveBeenCalledWith('flight_updated', handlers.onFlightUpdated);
    expect(signalR.connection.on).toHaveBeenCalledWith('flight_removed', expect.any(Function));
    expect(signalR.connection.on).toHaveBeenCalledWith('message_posted', handlers.onMessagePosted);
    expect(signalR.connection.onreconnected).toHaveBeenCalledWith(expect.any(Function));

    signalR.connection.onreconnected.mock.calls[0][0]();
    expect(handlers.onReconnected).toHaveBeenCalledOnce();
    signalR.handlers.get('flight_removed')?.({ id: 44, event_sequence: 91 } as never);
    expect(handlers.onFlightRemoved).toHaveBeenCalledWith(44, 91);

    await disconnect();
    expect(signalR.connection.stop).toHaveBeenCalledOnce();
  });
});
