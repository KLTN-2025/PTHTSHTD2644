document.addEventListener("DOMContentLoaded", function () {

    // --- 1. KHỐI LỆNH CHO PROFILE DROPDOWN (DESKTOP) ---
    const profileMenuToggleDesktop = document.getElementById("profileMenuToggleDesktop");
    const dropdownMenuDesktop = document.getElementById("dropdownMenuDesktop");

    if (profileMenuToggleDesktop && dropdownMenuDesktop) {
        profileMenuToggleDesktop.addEventListener("click", e => {
            e.stopPropagation();
            dropdownMenuDesktop.classList.toggle("hidden");
        });
        document.addEventListener("click", e => {
            if (profileMenuToggleDesktop && !profileMenuToggleDesktop.contains(e.target) && dropdownMenuDesktop && !dropdownMenuDesktop.contains(e.target)) {
                dropdownMenuDesktop.classList.add("hidden");
            }
        });
    }

    // --- 2. KHỐI LỆNH CHO CHAT AI ---
    const chatOpen = document.getElementById("chatOpen");
    const chatClose = document.getElementById("chatClose");
    const chatContainer = document.getElementById("chatContainer");
    const chatSend = document.getElementById("chatSend");
    const chatInput = document.getElementById("chatInput");
    const chatMessages = document.getElementById("chatMessages");

    if (chatOpen) chatOpen.addEventListener("click", () => chatContainer?.classList.remove("hidden"));
    if (chatClose) chatClose.addEventListener("click", () => chatContainer?.classList.add("hidden"));

    // HÀM THÊM TIN NHẮN (ĐÃ BỎ COMMENT VÀ HOÀN THIỆN)
    function appendMessage(sender, text) {
        if (!chatMessages) return;
        const div = document.createElement("div");
        div.className = sender === "Bạn" ? "text-right" : "text-left";
        const formattedText = text.replace(/\n/g, '<br>');
        div.innerHTML = `<div class="inline-block px-4 py-2 rounded-lg ${sender === "Bạn" ? "bg-blue-600 text-white" : "bg-gray-200 text-gray-800"}"><b>${sender}:</b> ${formattedText}</div>`;
        chatMessages.appendChild(div);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    // HÀM GỬI TIN NHẮN (ĐÃ BỎ COMMENT VÀ HOÀN THIỆN)
    async function sendMessage() {
        if (!chatInput || !chatSend || !chatMessages) return;
        const msg = chatInput.value.trim();
        if (!msg) return;
        appendMessage("Bạn", msg);
        chatInput.value = "";

        const originalIcon = chatSend.innerHTML;
        chatSend.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
        chatSend.disabled = true;
        chatInput.disabled = true;

        try {
            const response = await fetch("/Chat/SendMessage", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ message: msg })
            });
            if (!response.ok) throw new Error("Server error");
            const data = await response.json();
            appendMessage("AI", data.reply || "Xin lỗi, tôi chưa thể trả lời.");
        } catch (err) {
            appendMessage("AI", "❌ Lỗi kết nối.");
            console.error(err);
        } finally {
            chatSend.innerHTML = originalIcon;
            chatSend.disabled = false;
            chatInput.disabled = false;
            chatInput.focus();
        }
    }

    if (chatSend) chatSend.addEventListener("click", sendMessage);
    if (chatInput) chatInput.addEventListener("keypress", e => { if (e.key === "Enter") { e.preventDefault(); sendMessage(); } });

    // --- 3. KHỐI LỆNH CHO "NHÀ HÀNG GẦN BẠN" ---
    const nearbyBtnDesktop = document.getElementById('nearbyBtnDesktop');
    const nearbyBtnMobile = document.getElementById('nearbyBtnMobile');
    const nearbyModal = document.getElementById("nearbyModal");
    const nearbyModalClose = document.getElementById("nearbyModalClose");
    const nearbyList = document.getElementById("nearbyList");
    const mobileMenu = document.getElementById('mobileMenu');
    const mobileMenuButton = document.getElementById('mobileMenuButton'); // Thêm nút hamburger

    async function openNearbyModalAndSearch() {
        if (!nearbyModal || !nearbyList) return;
        nearbyModal.classList.remove("hidden");
        nearbyList.innerHTML = `<div class="flex flex-col items-center justify-center p-8 text-center"><i class="fas fa-spinner fa-spin text-4xl text-blue-500"></i><p class="mt-3 text-gray-600">Đang tìm vị trí...</p></div>`;

        if (navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(async (position) => {
                const nearbyStatusP = nearbyList.querySelector("p");
                if (nearbyStatusP) nearbyStatusP.textContent = "Đang tìm nhà hàng gần bạn...";
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
                            item.className = "p-3 border border-gray-200 rounded-lg hover:shadow-md transition-shadow cursor-pointer";
                            item.innerHTML = `
                                <div class="flex justify-between items-center mb-1">
                                    <h4 class="text-base font-semibold text-blue-600">${r.Name || 'N/A'}</h4>
                                    <span class="text-sm font-medium text-green-600">${r.Distance ? r.Distance.toFixed(1) + ' km' : ''}</span>
                                </div>
                                <p class="text-xs text-gray-500">${r.Address || 'N/A'}</p>`;
                            item.onclick = () => { window.location.href = `/Home/RestaurantDetails/${r.Id}`; };
                            nearbyList.appendChild(item);
                        });
                    }
                } catch (err) { nearbyList.innerHTML = `<p class="text-red-500 text-center p-4">Lỗi khi lấy danh sách.</p>`; console.error(err); }
            }, (err) => {
                let message = "Vui lòng cho phép truy cập vị trí.";
                if (err.code === 1) message = "Bạn đã từ chối quyền truy cập vị trí.";
                if (err.code === 2) message = "Không thể xác định vị trí.";
                if (err.code === 3) message = "Yêu cầu vị trí đã hết hạn.";
                nearbyList.innerHTML = `<p class="text-red-500 text-center p-4">${message}</p>`;
            }, { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 });
        } else { nearbyList.innerHTML = `<p class="text-yellow-600 text-center p-4">Trình duyệt không hỗ trợ định vị.</p>`; }
    }

    if (nearbyBtnDesktop) nearbyBtnDesktop.addEventListener('click', openNearbyModalAndSearch);
    if (nearbyBtnMobile) {
        nearbyBtnMobile.addEventListener('click', () => {
            openNearbyModalAndSearch();
            if (mobileMenu) mobileMenu.classList.add('hidden');
            const hamburgerIcon = mobileMenuButton?.querySelector('i');
            if (hamburgerIcon) {
                hamburgerIcon.classList.remove('fa-times');
                hamburgerIcon.classList.add('fa-bars');
            }
        });
    }

    if (nearbyModalClose) nearbyModalClose.addEventListener("click", () => nearbyModal?.classList.add("hidden"));
    if (nearbyModal) nearbyModal.addEventListener("click", (e) => { if (e.target === nearbyModal) { nearbyModal.classList.add("hidden"); } });

    // --- 4. KHỐI LỆNH CHO MENU MOBILE ---
    // const mobileMenuButton = document.getElementById('mobileMenuButton'); // Đã khai báo ở trên
    // const mobileMenu = document.getElementById('mobileMenu'); // Đã khai báo ở trên

    if (mobileMenuButton && mobileMenu) {
        mobileMenuButton.addEventListener('click', () => {
            mobileMenu.classList.toggle('hidden');
            const icon = mobileMenuButton.querySelector('i');
            if (icon) {
                icon.classList.toggle('fa-bars');
                icon.classList.toggle('fa-times');
            }
        });

        mobileMenu.querySelectorAll('a, button').forEach(item => {
            if (item.id !== 'nearbyBtnMobile') {
                item.addEventListener('click', () => {
                    mobileMenu.classList.add('hidden');
                    const icon = mobileMenuButton.querySelector('i');
                    if (icon) {
                        icon.classList.remove('fa-times');
                        icon.classList.add('fa-bars');
                    }
                });
            }
        });
    }

}); // Kết thúc DOMContentLoaded