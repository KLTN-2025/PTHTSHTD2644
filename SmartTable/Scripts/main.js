document.addEventListener("DOMContentLoaded", function () {

    // ==========================================
    // 1. UTILS (CÁC HÀM TIỆN ÍCH)
    // ==========================================
    function escapeHtml(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function getAntiForgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        if (input && input.value) return input.value;
        return null;
    }

    // ==========================================
    // 2. MENU PROFILE (DESKTOP)
    // ==========================================
    const profileMenuToggleDesktop = document.getElementById("profileMenuToggleDesktop");
    const dropdownMenuDesktop = document.getElementById("dropdownMenuDesktop");

    if (profileMenuToggleDesktop && dropdownMenuDesktop) {
        profileMenuToggleDesktop.addEventListener("click", function (e) {
            e.stopPropagation();
            dropdownMenuDesktop.classList.toggle("hidden");
        });
        document.addEventListener("click", function (e) {
            if (!profileMenuToggleDesktop.contains(e.target) && !dropdownMenuDesktop.contains(e.target)) {
                dropdownMenuDesktop.classList.add("hidden");
            }
        });
    }

    // ==========================================
    // 3. CHAT AI (CÓ YÊU CẦU THÔNG TIN)
    // ==========================================
    const chatOpen = document.getElementById("chatOpen");
    const chatClose = document.getElementById("chatClose");
    const chatContainer = document.getElementById("chatContainer");

    // Các phần tử Form nhập thông tin
    const chatInfoForm = document.getElementById("chatInfoForm");
    const chatMainInterface = document.getElementById("chatMainInterface");
    const btnStartChat = document.getElementById("btnStartChat");
    const guestNameInput = document.getElementById("guestName");
    const guestPhoneInput = document.getElementById("guestPhone");

    // Các phần tử khung Chat chính
    const chatSend = document.getElementById("chatSend");
    const chatInput = document.getElementById("chatInput");
    const chatMessages = document.getElementById("chatMessages");

    let currentUserInfo = null; // Lưu thông tin khách

    // Mở Chat
    if (chatOpen && chatContainer) {
        chatOpen.addEventListener("click", function () {
            chatContainer.classList.remove("hidden");
            checkChatSession(); // Kiểm tra xem đã nhập thông tin chưa
        });
    }

    // Đóng Chat
    if (chatClose && chatContainer) {
        chatClose.addEventListener("click", function () {
            chatContainer.classList.add("hidden");
        });
    }

    // Kiểm tra Session Storage
    function checkChatSession() {
        const storedName = sessionStorage.getItem("chat_guest_name");
        const storedPhone = sessionStorage.getItem("chat_guest_phone");

        if (storedName && storedPhone) {
            // Đã có thông tin -> Vào thẳng Chat
            currentUserInfo = { name: storedName, phone: storedPhone };
            showChatInterface();
        } else {
            // Chưa có -> Hiện Form nhập liệu
            if (chatInfoForm) chatInfoForm.classList.remove("hidden");
            if (chatMainInterface) chatMainInterface.classList.add("hidden");
        }
    }

    // Xử lý nút "BẮT ĐẦU CHÁT"
    if (btnStartChat) {
        btnStartChat.addEventListener("click", function () {
            const name = guestNameInput.value.trim();
            const phone = guestPhoneInput.value.trim();

            if (!name || !phone) {
                alert("Vui lòng nhập Tên và Số điện thoại để bắt đầu!");
                return;
            }

            // Lưu vào Session Storage
            sessionStorage.setItem("chat_guest_name", name);
            sessionStorage.setItem("chat_guest_phone", phone);

            currentUserInfo = { name: name, phone: phone };
            showChatInterface();
        });
    }

    // Hiển thị giao diện Chat chính
    function showChatInterface() {
        if (chatInfoForm) chatInfoForm.classList.add("hidden");
        if (chatMainInterface) {
            chatMainInterface.classList.remove("hidden");
            chatMainInterface.classList.add("flex"); // Cần flex để layout đúng
        }

        // Gửi lời chào nếu chưa có tin nhắn
        if (chatMessages && chatMessages.children.length === 0) {
            appendMessage("AI", `Chào <b>${escapeHtml(currentUserInfo.name)}</b>! 👋<br>SmartTable có thể giúp gì cho bạn hôm nay?`);
        }
    }

    // Hàm hiển thị tin nhắn (Hỗ trợ Ảnh Markdown)
    function appendMessage(sender, text) {
        if (!chatMessages) return;
        var div = document.createElement("div");
        div.className = (sender === "Bạn" || sender === "user") ? "text-right" : "text-left";

        let formattedText = escapeHtml(text);

        // Xử lý ảnh Markdown: ![Alt](URL)
        const imgRegex = /!\[(.*?)\]\((.*?)\)/g;
        formattedText = formattedText.replace(imgRegex, function (match, alt, url) {
            return `<br><a href="${url}" target="_blank"><img src="${url}" alt="${alt}" class="max-w-full h-auto rounded-lg mt-2 mb-2 shadow-md hover:opacity-90 transition" style="max-height: 200px; object-fit: cover;" onerror="this.style.display='none'" /></a><br>`;
        });

        formattedText = formattedText.replace(/\n/g, '<br>');

        var safeSender = escapeHtml(sender === "ai" || sender === "AI" ? "Trợ lý ảo" : sender);

        div.innerHTML = `
            <div class="inline-block px-4 py-2 rounded-lg text-sm ${sender === 'Bạn' || sender === 'user' ? 'bg-blue-600 text-white rounded-tr-none' : 'bg-gray-200 text-gray-800 rounded-tl-none'}">
                <strong>${safeSender}:</strong><br>
                ${formattedText}
            </div>
        `;
        chatMessages.appendChild(div);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    // Hàm gửi tin nhắn đến Server
    async function sendMessage() {
        if (!chatInput || !chatSend) return;

        var msg = chatInput.value.trim();
        if (!msg) return;

        appendMessage("Bạn", msg);
        chatInput.value = "";

        var originalIcon = chatSend.innerHTML;
        chatSend.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
        chatSend.disabled = true;
        chatInput.disabled = true;

        try {
            var headers = { "Content-Type": "application/json" };
            var token = getAntiForgeryToken();
            if (token) headers["RequestVerificationToken"] = token;

            // Gửi kèm thông tin user (nếu cần lưu lead ở server)
            var payload = {
                message: msg,
                userName: currentUserInfo ? currentUserInfo.name : null,
                userPhone: currentUserInfo ? currentUserInfo.phone : null
            };

            var response = await fetch("/Chat/SendMessage", {
                method: "POST",
                headers: headers,
                body: JSON.stringify(payload)
            });

            if (!response.ok) throw new Error("Lỗi server: " + response.status);

            var data = await response.json();
            if (data.success) {
                appendMessage("AI", data.reply);
            } else {
                appendMessage("AI", "⚠️ " + (data.reply || "Lỗi không xác định."));
            }
        } catch (err) {
            appendMessage("AI", "❌ Lỗi kết nối mạng.");
            console.error(err);
        } finally {
            chatSend.innerHTML = originalIcon;
            chatSend.disabled = false;
            chatInput.disabled = false;
            chatInput.focus();
        }
    }

    if (chatSend) chatSend.addEventListener("click", sendMessage);
    if (chatInput) chatInput.addEventListener("keypress", function (e) { if (e.key === "Enter") { e.preventDefault(); sendMessage(); } });


    // ==========================================
    // 4. TÍNH NĂNG "NHÀ HÀNG GẦN BẠN"
    // ==========================================
    const nearbyBtnDesktop = document.getElementById('nearbyBtnDesktop');
    const nearbyBtnMobile = document.getElementById('nearbyBtnMobile');
    const nearbyModal = document.getElementById("nearbyModal");
    const nearbyModalClose = document.getElementById("nearbyModalClose");
    const nearbyList = document.getElementById("nearbyList");
    const mobileMenu = document.getElementById('mobileMenu');
    const mobileMenuButton = document.getElementById('mobileMenuButton');

    async function openNearbyModalAndSearch() {
        if (!nearbyModal || !nearbyList) return;
        nearbyModal.classList.remove("hidden");
        nearbyList.innerHTML = '<div class="flex flex-col items-center justify-center p-8 text-center"><i class="fas fa-spinner fa-spin text-4xl text-blue-500"></i><p class="mt-3 text-gray-600">Đang xác định vị trí...</p></div>';

        if (navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(async function (position) {
                var nearbyStatusP = nearbyList.querySelector("p");
                if (nearbyStatusP) nearbyStatusP.textContent = "Đang tìm nhà hàng gần bạn...";
                var lat = position.coords.latitude;
                var lng = position.coords.longitude;
                try {
                    var url = '/PublicRestaurant/GetNearbyMapData?lat=' + encodeURIComponent(lat) + '&lng=' + encodeURIComponent(lng) + '&radiusKm=5';
                    var response = await fetch(url);
                    if (!response.ok) throw new Error("Lỗi mạng: " + response.status);
                    var data = await response.json();
                    nearbyList.innerHTML = "";
                    if (!data || data.length === 0) {
                        nearbyList.innerHTML = '<p class="text-gray-500 text-center p-4">Không tìm thấy nhà hàng nào gần bạn.</p>';
                    } else {
                        data.forEach(function (r) {
                            var name = r.Name || r.name || 'N/A';
                            var address = r.Address || r.address || 'N/A';
                            var id = r.Id || r.restaurant_id || r.restaurantId || '';
                            var dist = (r.Distance !== undefined && r.Distance !== null) ? Number(r.Distance) : (r.distanceKm !== undefined ? Number(r.distanceKm) : null);

                            var item = document.createElement("div");
                            item.className = "p-3 border border-gray-200 rounded-lg hover:shadow-md transition-shadow cursor-pointer bg-white mb-2";
                            item.innerHTML = `
                                <div class="flex justify-between items-center mb-1">
                                    <h4 class="text-base font-semibold text-blue-600">${escapeHtml(name)}</h4>
                                    <span class="text-sm font-medium text-green-600">${dist !== null ? dist.toFixed(1) + " km" : ""}</span>
                                </div>
                                <p class="text-xs text-gray-500 truncate"><i class="fas fa-map-marker-alt mr-1"></i>${escapeHtml(address)}</p>
                            `;
                            item.onclick = function () {
                                if (id) window.location.href = '/PublicRestaurant/Details/' + encodeURIComponent(id);
                            };
                            nearbyList.appendChild(item);
                        });
                    }
                } catch (err) {
                    nearbyList.innerHTML = '<p class="text-red-500 text-center p-4">Lỗi kết nối.</p>';
                    console.error(err);
                }
            }, function (err) {
                var message = "Vui lòng cho phép truy cập vị trí.";
                if (err && err.code === 1) message = "Bạn đã từ chối quyền truy cập vị trí.";
                nearbyList.innerHTML = '<p class="text-red-500 text-center p-4">' + escapeHtml(message) + '</p>';
            }, { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 });
        } else {
            nearbyList.innerHTML = '<p class="text-yellow-600 text-center p-4">Trình duyệt không hỗ trợ định vị.</p>';
        }
    }

    if (nearbyBtnDesktop) nearbyBtnDesktop.addEventListener('click', openNearbyModalAndSearch);
    if (nearbyBtnMobile) {
        nearbyBtnMobile.addEventListener('click', function () {
            openNearbyModalAndSearch();
            if (mobileMenu) mobileMenu.classList.add('hidden');
            if (mobileMenuButton) {
                var icon = mobileMenuButton.querySelector('i');
                if (icon) { icon.classList.remove('fa-times'); icon.classList.add('fa-bars'); }
            }
        });
    }

    if (nearbyModalClose) nearbyModalClose.addEventListener("click", function () { if (nearbyModal) nearbyModal.classList.add("hidden"); });
    if (nearbyModal) nearbyModal.addEventListener("click", function (e) { if (e.target === nearbyModal) { nearbyModal.classList.add("hidden"); } });

    // --- 5. MOBILE MENU ---
    if (mobileMenuButton && mobileMenu) {
        mobileMenuButton.addEventListener('click', function () {
            mobileMenu.classList.toggle('hidden');
            var icon = mobileMenuButton.querySelector('i');
            if (icon) {
                icon.classList.toggle('fa-bars');
                icon.classList.toggle('fa-times');
            }
        });
        mobileMenu.querySelectorAll('a, button').forEach(function (item) {
            if (item.id !== 'nearbyBtnMobile') {
                item.addEventListener('click', function () {
                    mobileMenu.classList.add('hidden');
                    var icon = mobileMenuButton.querySelector('i');
                    if (icon) { icon.classList.remove('fa-times'); icon.classList.add('fa-bars'); }
                });
            }
        });
    }

});