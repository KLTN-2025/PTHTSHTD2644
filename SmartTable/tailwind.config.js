/** @type {import('tailwindcss').Config} */
module.exports = {
    content: [
        './Views/**/*.cshtml', // Keep this line
        './Views/Shared/_Layout.cshtml' // Add this explicit path too
    ],
    theme: {
        extend: {},
    },
    plugins: [],
}