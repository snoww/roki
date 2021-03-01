const defaultTheme = require('tailwindcss/defaultTheme')

module.exports = {
  purge: [
    './Views/**/*.chstml'
  ],
  darkMode: false, // or 'media' or 'class'
  theme: {
    extend: {
      fontFamily: {
        sans: ['Poppins', ...defaultTheme.fontFamily.sans],
      },
    },
  },
  variants: {
    extend: {},
  },
  plugins: [],
}
