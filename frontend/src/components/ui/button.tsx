import type { ButtonHTMLAttributes } from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '../../lib/utils'

const buttonVariants = cva(
  'inline-flex items-center justify-center rounded-[5px] border text-xs font-semibold transition-[background-color,transform] duration-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#111] disabled:pointer-events-none disabled:opacity-45 active:scale-[0.98]',
  {
    variants: {
      variant: {
        default: 'border-[#111] bg-[#111] text-white hover:bg-[#333]',
        outline: 'border-[#dededb] bg-white text-[#2f3437] hover:bg-[#f7f6f3]',
        ghost: 'border-transparent bg-transparent text-[#1f6c9f] hover:bg-[#e1f3fe]',
      },
      size: {
        default: 'h-10 px-4',
        sm: 'h-8 px-3 text-[11px]',
      },
    },
    defaultVariants: { variant: 'default', size: 'default' },
  },
)

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & VariantProps<typeof buttonVariants>

function Button({ className, variant, size, type = 'button', ...props }: ButtonProps) {
  return <button type={type} className={cn(buttonVariants({ variant, size }), className)} {...props} />
}

export { Button, buttonVariants }
