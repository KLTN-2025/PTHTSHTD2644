/* main.js - Chứa tất cả logic tùy chỉnh cho _Layout.cshtml */

// Chạy code khi trang đã tải xong
document.addEventListener("DOMContentLoaded", function () {

    // --- 1. KHỐI LỆNH CHO PROFILE DROPDOWN ---
    const profileMenuToggle = document.getElementById("profileMenuToggle");
    const dropdownMenu = document.getElementById("dropdownMenu");

    if (profileMenuToggle && dropdownMenu) {
        // Bấm vào nút để Bật/Tắt menu
        profileMenuToggle.addEventListener("click", e => {
            e.stopPropagation(); // Ngăn sự kiện click lan ra document
            dropdownMenu.classList.toggle("hidden");
        });

        // Tự động đóng khi bấm ra ngoài
        document.addEventListener("click", e => {
            if (!profileMenuToggle.contains(e.target) && !dropdownMenu.contains(e.target)) {
                dropdownMenu.classList.add("hidden");
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

    if (chatOpen) chatOpen.addEventListener("click", () => chatContainer.classList.remove("hidden"));
    if (chatClose) chatClose.addEventListener("click", () => chatContainer.classList.add("hidden"));

    // Hàm thêm tin nhắn vào khung chat
    function appendMessage(sender, text) {
        const div = document.createElement("div");
        div.className = sender === "Bạn" ? "text-right" : "text-left";

        // Xử lý text để hiển thị xuống dòng (thay \n bằng <br>)
        const formattedText = text.replace(/\n/g, '<br>');

        div.innerHTML = `<div class="inline-block px-4 py-2 rounded-lg ${sender === "Bạn" ? "bg-blue-600 text-white" : "bg-gray-200 text-gray-800"}">
                            <b>${sender}:</b> ${formattedText}
                         </div>`;
        chatMessages.appendChild(div);
        chatMessages.scrollTop = chatMessages.scrollHeight; // Tự cuộn xuống cuối
    }

    // Hàm gửi tin nhắn
    async function sendMessage() {
        const msg = chatInput.value.trim();
        if (!msg) return;

        appendMessage("Bạn", msg);
        chatInput.value = "";

        // NÂNG CẤP UX: Thêm trạng thái chờ (loading) cho nút gửi
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
            appendMessage("AI", data.reply || "❌ AI chưa trả lời được");
        } catch (err) {
            appendMessage("AI", "❌ Lỗi kết nối. Vui lòng thử lại.");
            console.error(err);
        } finally {
            // Hoàn tất, trả lại nút bấm
            chatSend.innerHTML = originalIcon;
            chatSend.disabled = false;
            chatInput.disabled = false;
            chatInput.focus();
        }
    }

    if (chatSend) chatSend.addEventListener("click", sendMessage);
    if (chatInput) chatInput.addEventListener("keypress", e => { if (e.key === "Enter") { e.preventDefault(); sendMessage(); } });

    // --- 3. KHỐI LỆNH CHO "NHÀ HÀNG GẦN BẠN" (DÙNG MODAL) ---
    const nearbyBtn = document.getElementById("nearbyBtn");
    const nearbyModal = document.getElementById("nearbyModal");
    const nearbyModalClose = document.getElementById("nearbyModalClose");
    const nearbyList = document.getElementById("nearbyList");

    if (nearbyBtn && nearbyModal && nearbyModalClose && nearbyList) {

        // Bấm nút để Mở Modal và bắt đầu tìm kiếm
        nearbyBtn.addEventListener("click", () => {
            nearbyModal.classList.remove("hidden");
            findNearbyRestaurants(); // Bắt đầu tìm kiếm ngay khi mở
        });

        // Đóng Modal (bằng nút X)
        nearbyModalClose.addEventListener("click", () => nearbyModal.classList.add("hidden"));

        // Đóng Modal (bằng cách bấm vào nền mờ bên ngoài)
        nearbyModal.addEventListener("click", (e) => {
            if (e.target === nearbyModal) { // Chỉ đóng khi bấm vào nền mờ
                nearbyModal.classList.add("hidden");
            }
        });

        // Hàm tìm nhà hàng (tách riêng logic)
        async function findNearbyRestaurants() {
            // NÂNG CẤP UX: Thêm trạng thái chờ (loading) vào modal body
            nearbyList.innerHTML = `
                <div class="flex flex-col items-center justify-center p-8 text-center">
                    <i class="fas fa-spinner fa-spin text-4xl text-blue-500"></i>
                    <p class="mt-3 text-gray-600">Đang tìm vị trí của bạn...</p>
                </div>`;

            if (navigator.geolocation) {
                navigator.geolocation.getCurrentPosition(async (position) => {
                    // Cập nhật trạng thái
                    nearbyList.querySelector("p").textContent = "Đang tìm nhà hàng gần bạn...";

                    const lat = position.coords.latitude;
                    const lng = position.coords.longitude;

                    try {
                        const response = await fetch(`/Home/GetNearbyRestaurants?lat=${lat}&lng=${lng}`);
                        if (!response.ok) throw new Error("Network response was not ok");
                        const data = await response.json();

                        nearbyList.innerHTML = ""; // Xóa loading

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
                                    <p class="text-sm text-gray-600">${r.Address}</p>
                                `;
                                // Thêm sự kiện click để điều hướng (Cần cập nhật link này cho đúng)
                                item.onclick = () => {
                                    // BẠN CẦN CẬP NHẬT LINK NÀY CHO ĐÚNG
                                    window.location.href = `/Restaurant/Details/${r.Id}`;
                                };
                                nearbyList.appendChild(item);
                            });
                        }
                    } catch (err) {
                        console.error(err);
                        nearbyList.innerHTML = `<p class="text-red-500 text-center p-4">Lỗi khi lấy danh sách nhà hàng. Vui lòng thử lại.</p>`;
                    }
                }, (err) => {
                    // Lỗi khi người dùng từ chối cấp quyền
                    nearbyList.innerHTML = `<p class="text-red-500 text-center p-4">Vui lòng bật định vị (hoặc cấp quyền) để sử dụng chức năng này.</p>`;
                });
            } else {
                // Lỗi khi trình duyệt không hỗ trợ
                nearbyList.innerHTML = `<p class="text-red-500 text-center p-4">Trình duyệt của bạn không hỗ trợ định vị.</p>`;
            }
        }
    }
});