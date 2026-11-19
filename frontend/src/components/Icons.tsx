import type { SVGProps } from 'react';

type IconName =
  | 'aircraft'
  | 'power'
  | 'cooling'
  | 'temperature'
  | 'communication'
  | 'computer'
  | 'equipment'
  | 'play'
  | 'pause'
  | 'restart'
  | 'download'
  | 'filter'
  | 'clock'
  | 'database'
  | 'shield'
  | 'chevron';

interface IconProps extends SVGProps<SVGSVGElement> {
  name: IconName;
}

export function Icon({ name, ...props }: IconProps) {
  const common = {
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 1.8,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    'aria-hidden': true,
    ...props,
  };

  switch (name) {
    case 'aircraft':
      return <svg {...common}><path d="m21 16-8.7-3.4V6.5c0-2.1-.9-3.5-1.8-3.5S8.7 4.4 8.7 6.5v6.1L3 15l-1.7-1.2-.6 1.1L4 18l4.7-.9v2.4l-1.5 1.2V22l3.3-.6 3.3.6v-1.3l-1.5-1.2v-2.4l8.7.9v-2Z" /></svg>;
    case 'power':
      return <svg {...common}><path d="M13 2 4.5 13h6L9.8 22 19.5 10h-6L13 2Z" /></svg>;
    case 'cooling':
      return <svg {...common}><path d="M12 3v18M4.2 7.5l15.6 9M4.2 16.5l15.6-9M9.5 4.5 12 7l2.5-2.5M9.5 19.5 12 17l2.5 2.5M4.7 10.9l3.4-.9-.9-3.4M19.3 13.1l-3.4.9.9 3.4" /></svg>;
    case 'temperature':
      return <svg {...common}><path d="M14 14.8V5a4 4 0 0 0-8 0v9.8a5 5 0 1 0 8 0Z" /><path d="M10 8v9" /></svg>;
    case 'communication':
      return <svg {...common}><path d="M5.6 8.2a9 9 0 0 0 0 7.6M9 10.5a5 5 0 0 0 0 3M18.4 8.2a9 9 0 0 1 0 7.6M15 10.5a5 5 0 0 1 0 3" /><circle cx="12" cy="12" r="1.4" /></svg>;
    case 'computer':
      return <svg {...common}><rect x="3" y="4" width="18" height="13" rx="2" /><path d="M8 21h8M12 17v4M7 8h5M7 11h3" /></svg>;
    case 'equipment':
      return <svg {...common}><path d="M12 2.5 20 7v10l-8 4.5L4 17V7l8-4.5Z" /><path d="m8.5 12 2.2 2.2 4.8-5" /></svg>;
    case 'play':
      return <svg {...common}><path d="m8 5 11 7-11 7V5Z" /></svg>;
    case 'pause':
      return <svg {...common}><path d="M9 5v14M15 5v14" /></svg>;
    case 'restart':
      return <svg {...common}><path d="M4 8V3m0 0h5M4 3l3.3 3.3A8 8 0 1 1 4.7 15" /></svg>;
    case 'download':
      return <svg {...common}><path d="M12 3v12m0 0 4-4m-4 4-4-4M5 21h14" /></svg>;
    case 'filter':
      return <svg {...common}><path d="M3 5h18l-7 8v5l-4 2v-7L3 5Z" /></svg>;
    case 'clock':
      return <svg {...common}><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></svg>;
    case 'database':
      return <svg {...common}><ellipse cx="12" cy="5" rx="8" ry="3" /><path d="M4 5v7c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 12v7c0 1.7 3.6 3 8 3s8-1.3 8-3v-7" /></svg>;
    case 'shield':
      return <svg {...common}><path d="M12 2.5 20 6v5.5c0 5-3.4 8.4-8 10-4.6-1.6-8-5-8-10V6l8-3.5Z" /><path d="m8.5 12 2.2 2.2 4.8-5" /></svg>;
    case 'chevron':
      return <svg {...common}><path d="m9 7 5 5-5 5" /></svg>;
  }
}
