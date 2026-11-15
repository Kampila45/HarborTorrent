import { forwardRef, type ButtonHTMLAttributes } from 'react';

export type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'danger' | 'dark' | 'ghost';
export type ButtonSize = 'xs' | 'sm' | 'md';

const variantClasses: Record<ButtonVariant, string> = {
  primary: 'bg-[#2383E2] text-white border border-transparent hover:bg-[#1C68B3]',
  secondary:
    'bg-[#F7F7F5] dark:bg-[#2A2A2A] text-[#37352F] dark:text-[#E9E9E7] border border-[#DCDAD7] dark:border-[#444444] hover:bg-[#EDEDEB] dark:hover:bg-[#333333]',
  outline:
    'bg-transparent text-[#37352F] dark:text-[#E9E9E7] border border-[#E9E9E7] dark:border-[#333333] hover:bg-[#F7F7F5] dark:hover:bg-[#1A1A1A]',
  danger: 'bg-[#D4403A] text-white border border-transparent hover:bg-[#B83530]',
  dark: 'bg-[#37352F] dark:bg-[#E9E9E7] text-white dark:text-[#111111] border border-transparent hover:bg-[#2F2D28] dark:hover:bg-white',
  ghost:
    'bg-transparent text-[#5F5E5B] dark:text-[#A0A0A0] border border-transparent hover:bg-[#F7F7F5] dark:hover:bg-[#222222]',
};

const sizeClasses: Record<ButtonSize, string> = {
  xs: 'h-7 px-2.5 text-[12px] rounded gap-1',
  sm: 'h-8 px-3 text-[13px] rounded-md gap-1.5',
  md: 'h-9 px-4 text-[13px] rounded-md gap-2',
};

export function getButtonClassName({
  variant = 'primary',
  size = 'sm',
  className = '',
}: {
  variant?: ButtonVariant;
  size?: ButtonSize;
  className?: string;
} = {}): string {
  return [
    'inline-flex items-center justify-center whitespace-nowrap font-medium transition-colors active:scale-[0.98] disabled:cursor-not-allowed disabled:opacity-50',
    variantClasses[variant],
    sizeClasses[size],
    className,
  ]
    .filter(Boolean)
    .join(' ');
}

export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  size?: ButtonSize;
};

/**
 * Shared button primitive keeping padding, font size, radius, and focus/disabled states uniform.
 */
export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'primary', size = 'sm', type = 'button', className = '', children, ...props },
  ref,
) {
  return (
    <button
      ref={ref}
      type={type}
      className={getButtonClassName({ variant, size, className })}
      {...props}
    >
      {children}
    </button>
  );
});

