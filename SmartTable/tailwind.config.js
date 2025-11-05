module.exports = {
    content: [
        './Views/**/*.cshtml',  // Đảm bảo rằng tất cả các file .cshtml đều được bao gồm
        './wwwroot/**/*.html',  // Nếu có các file HTML trong wwwroot
        './wwwroot/js/**/*.js'  // Nếu có các file JavaScript có sử dụng Tailwind
    ],
    safelist: [
        // Giữ lại các màu nút để không bị xóa khi build
        'bg-green-600', 'hover:bg-green-700',
        'bg-red-600', 'hover:bg-red-700',
        'bg-gray-600', 'hover:bg-gray-700',
        'text-white', 'text-sm', 'rounded-lg',
        'focus:ring-2', 'focus:ring-green-500',
        'focus:ring-red-500', 'focus:ring-gray-500',
    ],
    theme: {
        extend: {},
    },
    plugins: [],
}
