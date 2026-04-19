import type { PresenceStatus } from '../signalr/ChatHubClient';

type PresenceBadgeProps = {
  status: PresenceStatus | undefined;
  size?: 'sm' | 'md';
  className?: string;
};

const GLYPHS = {
  Online: '\u25CF',
  AFK: '\u25D0',
  Offline: '\u25CB',
  Unknown: '\u25CB',
} as const;

const COLORS = {
  Online: 'text-emerald-400',
  AFK: 'text-amber-400',
  Offline: 'text-slate-500',
  Unknown: 'text-slate-600',
} as const;

const LABELS = {
  Online: 'Online',
  AFK: 'Away',
  Offline: 'Offline',
  Unknown: 'Offline',
} as const;

export function PresenceBadge({ status, size = 'sm', className }: PresenceBadgeProps) {
  const key = (status ?? 'Unknown') as keyof typeof GLYPHS;
  const fontSize = size === 'md' ? 'text-base' : 'text-xs';
  return (
    <span
      data-testid="presence-badge"
      data-presence={key}
      role="img"
      aria-label={LABELS[key]}
      title={LABELS[key]}
      className={`${COLORS[key]} ${fontSize} leading-none ${className ?? ''}`}
    >
      {GLYPHS[key]}
    </span>
  );
}
