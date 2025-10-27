document.addEventListener("DOMContentLoaded", function () {

    // --- 1. KHỐI LỆNH CHO PROFILE DROPDOWN (DESKTOP) ---
    const profileMenuToggleDesktop = document.getElementById("profileMenuToggleDesktop"); // ID mới
    const dropdownMenuDesktop = document.getElementById("dropdownMenuDesktop"); // ID mới

    if (profileMenuToggleDesktop && dropdownMenuDesktop) {
        profileMenuToggleDesktop.addEventListener("click", e => {
            e.stopPropagation();
            dropdownMenuDesktop.classList.toggle("hidden");
        });
        document.addEventListener("click", e => {
            if (profileMenuToggleDesktop && !profileMenuToggleDesktop.contains(e.target) && !dropdownMenuDesktop.contains(e.target)) {
                dropdownMenuDesktop.classList.add("hidden");
            }
        });
    }

    // --- 2. KHỐI LỆNH CHO CHAT AI (Giữ nguyên) ---
    const chatOpen = document.getElementById("chatOpen");
    const chatClose = document.getElementById("chatClose");
    const chatContainer = document.getElementById("chatContainer");
    const chatSend = document.getElementById("chatSend");
    const chatInput = document.getElementById("chatInput");
    const chatMessages = document.getElementById("chatMessages");

    if (chatOpen) chatOpen.addEventListener("click", () => chatContainer.classList.remove("hidden"));
    if (chatClose) chatClose.addEventListener("click", () => chatContainer.classList.add("hidden"));

    function appendMessage(sender, text) { /* ... (Giữ nguyên code appendMessage) ... */ }
    async function sendMessage() { /* ... (Giữ nguyên code sendMessage) ... */ }

    if (chatSend) chatSend.addEventListener("click", sendMessage);
    if (chatInput) chatInput.addEventListener("keypress", e => { if (e.key === "Enter") { e.preventDefault(); sendMessage(); } });

    // --- 3. KHỐI LỆNH CHO "NHÀ HÀNG GẦN BẠN" (DÙNG CẢ 2 NÚT) ---
    const nearbyBtnDesktop = document.getElementById('nearbyBtnDesktop'); // ID mới
    const nearbyBtnMobile = document.getElementById('nearbyBtnMobile');   // ID mới
    const nearbyModal = document.getElementById("nearbyModal");
    const nearbyModalClose = document.getElementById("nearbyModalClose");
    const nearbyList = document.getElementById("nearbyList");

    // Hàm mở Modal và tìm kiếm (có thể gọi từ cả 2 nút)
    async function openNearbyModalAndSearch() {
        if (!nearbyModal || !nearbyList) return;
        nearbyModal.classList.remove("hidden");
        nearbyList.innerHTML = `<div class="flex flex-col items-center justify-center p-8 text-center"><i class="fas fa-spinner fa-spin text-4xl text-blue-500"></i><p class="mt-3 text-gray-600">Đang tìm vị trí...</p></div>`;

        if (navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(async (position) => {
                nearbyList.querySelector("p").textContent = "Đang tìm nhà hàng gần bạn...";
                const lat = position.coords.latitude;
                const lng = position.coords.longitude;
                try {
                    const response = await fetch(`/Home/GetNearbyRestaurants?lat=${lat}&lng=${lng}`);
                    if (!response.ok) throw new Error("Network response was not ok");
                    const data = await response.json();
                    nearbyList.innerHTML = "";
                    if (data.length === 0) {
                        nearbyList.innerHTML = `<p class="text-gray-500 text-center p-4">Không tìm thấy nhà hàng nào gần bạn.</p>`;
                    } else {
                        data.forEach(r => {
                            const item = document.createElement("div");
                            item.className = "p-3 border rounded-lg hover:shadow-md transition-shadow cursor-pointer";
                            item.innerHTML = `
                                <div class="flex justify-between items-center">
                                    <h4 class="text-lg font-medium text-blue-600">${r.Name}</h4>
                                    <span class="text-sm font-medium text-green-600">${r.Distance.toFixed(1)} km</span>
                                </div>
                                <p class="text-sm text-gray-600">${r.Address}</p>`;
                            item.onclick = () => { window.location.href = `/Home/RestaurantDetails/${r.Id}`; }; // CẬP NHẬT LINK
                            nearbyList.appendChild(item);
                        });
                    }
                } catch (err) { nearbyList.innerHTML = `<p class="text-red-500 text-center p-4">Lỗi khi lấy danh sách.</p>`; console.error(err); }
            }, (err) => { nearbyList.innerHTML = `<p class="text-red-500 text-center p-4">Vui lòng bật định vị.</p>`; });
        } else { nearbyList.innerHTML = `<p class="text-red-500 text-center p-4">Trình duyệt không hỗ trợ.</p>`; }
    }

    // Gắn sự kiện cho cả 2 nút nearby
    if (nearbyBtnDesktop) {
        nearbyBtnDesktop.addEventListener('click', openNearbyModalAndSearch);
    }
    if (nearbyBtnMobile) {
        nearbyBtnMobile.addEventListener('click', () => {
            openNearbyModalAndSearch();
            // Ẩn menu mobile sau khi bấm nút nearby
            if (mobileMenu) mobileMenu.classList.add('hidden');
        });
    }

    // Code đóng Modal nearby (giữ nguyên)
    if (nearbyModalClose) nearbyModalClose.addEventListener("click", () => nearbyModal.classList.add("hidden"));
    if (nearbyModal) nearbyModal.addEventListener("click", (e) => { if (e.target === nearbyModal) { nearbyModal.classList.add("hidden"); } });

    // --- 4. KHỐI LỆNH MỚI CHO MENU MOBILE ---
    const mobileMenuButton = document.getElementById('mobileMenuButton');
    const mobileMenu = document.getElementById('mobileMenu');

    if (mobileMenuButton && mobileMenu) {
        mobileMenuButton.addEventListener('click', () => {
            mobileMenu.classList.toggle('hidden');
            // Đổi icon hamburger thành X và ngược lại (tùy chọn)
            const icon = mobileMenuButton.querySelector('i');
            if (icon) {
                icon.classList.toggle('fa-bars');
                icon.classList.toggle('fa-times');
            }
        });

        // (Tùy chọn) Tự động đóng menu mobile khi bấm vào link/nút bên trong
        mobileMenu.querySelectorAll('a, button').forEach(item => {
            if (item.id !== 'nearbyBtnMobile') { // Không đóng khi bấm nút nearby
                item.addEventListener('click', () => {
                    mobileMenu.classList.add('hidden');
                    // Đổi lại icon X thành hamburger
                    const icon = mobileMenuButton.querySelector('i');
                    if (icon) {
                        icon.classList.remove('fa-times');
                        icon.classList.add('fa-bars');
                    }
                });
            }
        });
    }

}); 