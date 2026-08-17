/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        base: {
          900: '#0a0d13',
          800: '#0f131b',
          700: '#151a23',
          600: '#1b212c',
          500: '#232a36'
        },
        line: 'rgba(255,255,255,0.06)'
      },
      boxShadow: {
        card: '0 1px 2px rgba(0,0,0,0.4), 0 8px 24px rgba(0,0,0,0.25)'
      },
      keyframes: {
        pulseRing: {
          '0%': { boxShadow: '0 0 0 0 rgba(239,68,68,0.45)' },
          '70%': { boxShadow: '0 0 0 10px rgba(239,68,68,0)' },
          '100%': { boxShadow: '0 0 0 0 rgba(239,68,68,0)' }
        }
      }
    }
  },
  plugins: []
}
