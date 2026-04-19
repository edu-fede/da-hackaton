import { describe, test, expect, beforeEach, afterEach, vi } from 'vitest';
import { act, render } from '@testing-library/react';
import { useHeartbeat } from './useHeartbeat';
import type { ChatHubClient } from '../signalr/ChatHubClient';

class FakeHub {
  state: 'Connected' | 'Connecting' = 'Connected';
  private resolvers: Array<() => void> = [];
  heartbeat = vi.fn(async () => undefined);
  whenConnected = vi.fn(async () => {
    if (this.state === 'Connected') return;
    return new Promise<void>((resolve) => this.resolvers.push(resolve));
  });
  markConnected() {
    this.state = 'Connected';
    const r = this.resolvers;
    this.resolvers = [];
    for (const f of r) f();
  }
}

function Harness({ hub }: { hub: FakeHub }) {
  useHeartbeat(hub as unknown as ChatHubClient);
  return null;
}

describe('useHeartbeat', () => {
  let hub: FakeHub;
  let visibilityState: 'visible' | 'hidden';
  let hasFocusReturn: boolean;

  beforeEach(() => {
    vi.useFakeTimers();
    hub = new FakeHub();
    visibilityState = 'visible';
    hasFocusReturn = true;
    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      get: () => visibilityState,
    });
    Object.defineProperty(document, 'hidden', {
      configurable: true,
      get: () => visibilityState === 'hidden',
    });
    vi.spyOn(document, 'hasFocus').mockImplementation(() => hasFocusReturn);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  function fireActivity() {
    act(() => {
      window.dispatchEvent(new Event('mousemove'));
    });
  }

  async function flushMicrotasks() {
    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });
  }

  test('emits a heartbeat on first activity after mount', async () => {
    render(<Harness hub={hub} />);
    await flushMicrotasks();

    fireActivity();
    await flushMicrotasks();

    expect(hub.heartbeat).toHaveBeenCalledTimes(1);
  });

  test('throttles multiple activity events within 12s to a single heartbeat', async () => {
    render(<Harness hub={hub} />);
    await flushMicrotasks();

    fireActivity();
    await flushMicrotasks();
    expect(hub.heartbeat).toHaveBeenCalledTimes(1);

    await act(async () => {
      vi.advanceTimersByTime(5_000);
    });
    fireActivity();
    await flushMicrotasks();
    fireActivity();
    await flushMicrotasks();

    expect(hub.heartbeat).toHaveBeenCalledTimes(1);

    await act(async () => {
      vi.advanceTimersByTime(7_001);
    });
    fireActivity();
    await flushMicrotasks();
    expect(hub.heartbeat).toHaveBeenCalledTimes(2);
  });

  test('does not emit while tab is hidden', async () => {
    render(<Harness hub={hub} />);
    await flushMicrotasks();

    visibilityState = 'hidden';
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'));
    });

    fireActivity();
    await flushMicrotasks();
    expect(hub.heartbeat).not.toHaveBeenCalled();
  });

  test('does not emit while window is blurred', async () => {
    render(<Harness hub={hub} />);
    await flushMicrotasks();

    hasFocusReturn = false;
    act(() => {
      window.dispatchEvent(new Event('blur'));
    });

    fireActivity();
    await flushMicrotasks();
    expect(hub.heartbeat).not.toHaveBeenCalled();
  });

  test('emits immediately on re-foreground (visibilitychange visible)', async () => {
    render(<Harness hub={hub} />);
    await flushMicrotasks();

    visibilityState = 'hidden';
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'));
    });
    fireActivity();
    await flushMicrotasks();
    expect(hub.heartbeat).not.toHaveBeenCalled();

    visibilityState = 'visible';
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'));
    });
    await flushMicrotasks();
    expect(hub.heartbeat).toHaveBeenCalledTimes(1);
  });

  test('waits for whenConnected before the first emission', async () => {
    hub.state = 'Connecting';
    render(<Harness hub={hub} />);
    await flushMicrotasks();

    fireActivity();
    await flushMicrotasks();
    expect(hub.heartbeat).not.toHaveBeenCalled();

    act(() => {
      hub.markConnected();
    });
    await flushMicrotasks();
    expect(hub.heartbeat).toHaveBeenCalledTimes(1);
  });
});
