document.addEventListener("DOMContentLoaded", function () {
    // ==========================
    // 1. UTILS
    // ==========================
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
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        if (input && input.value) return input.value;

        const meta = document.querySelector('meta[name="__RequestVerificationToken"]');
        if (meta && meta.content) return meta.content;

        return null;
    }

    // ==========================
    // 2. MENU PROFILE (DESKTOP)
    // ==========================
    const profileMenuToggleDesktop = document.getElementById("profileMenuToggleDesktop");
    const dropdownMenuDesktop = document.getElementById("dropdownMenuDesktop");

    if (profileMenuToggleDesktop && dropdownMenuDesktop) {
        profileMenuToggleDesktop.addEventListener("click", function (e) {
            e.stopPropagation();
            dropdownMenuDesktop.classList.toggle("hidden");
        });

        document.addEventListener("click", function (e) {
            if (!profileMenuToggleDesktop.contains(e.target) &&
                !dropdownMenuDesktop.contains(e.target)) {
                dropdownMenuDesktop.classList.add("hidden");
            }
        });
    }

    // ==========================
    // 3. CHAT AI
    // ==========================
    const chatOpen = document.getElementById("chatOpen");
    const chatClose = document.getElementById("chatClose");
    const chatContainer = document.getElementById("chatContainer");

    const chatInfoForm = document.getElementById("chatInfoForm");
    const chatMainInterface = document.getElementById("chatMainInterface");
    const btnStartChat = document.getElementById("btnStartChat");
    const guestNameInput = document.getElementById("guestName");
    const guestPhoneInput = document.getElementById("guestPhone");

    const chatSend = document.getElementById("chatSend");
    const chatInput = document.getElementById("chatInput");
    const chatMessages = document.getElementById("chatMessages");
    const chatClear = document.getElementById("chatClear");

    let currentUserInfo = null;

    // Mở chat
    if (chatOpen && chatContainer) {
        chatOpen.addEventListener("click", function () {
            chatContainer.classList.remove("hidden");
            checkChatSession();
        });
    }

    // Đóng chat
    if (chatClose && chatContainer) {
        chatClose.addEventListener("click", function () {
            chatContainer.classList.add("hidden");
        });
    }

    // Nút xóa đoạn chat
    if (chatClear && chatMessages) {
        chatClear.addEventListener("click", function () {
            chatMessages.innerHTML = "";
        });
    }

    // Kiểm tra session
    function checkChatSession() {
        const storedName = sessionStorage.getItem("chat_guest_name");
        const storedPhone = sessionStorage.getItem("chat_guest_phone");

        if (storedName && storedPhone) {
            currentUserInfo = { name: storedName, phone: storedPhone };
            showChatInterface();
        } else {
            if (chatInfoForm) chatInfoForm.classList.remove("hidden");
            if (chatMainInterface) chatMainInterface.classList.add("hidden");
        }
    }

    // Bắt đầu chat
    if (btnStartChat) {
        btnStartChat.addEventListener("click", function () {
            const name = guestNameInput.value.trim();
            const phone = guestPhoneInput.value.trim();

            if (!name || !phone) {
                alert("Vui lòng nhập Tên và Số điện thoại để bắt đầu!");
                return;
            }

            sessionStorage.setItem("chat_guest_name", name);
            sessionStorage.setItem("chat_guest_phone", phone);

            currentUserInfo = { name, phone };
            showChatInterface();
        });
    }

    // Hiện giao diện chat chính
    function showChatInterface() {
        if (chatInfoForm) chatInfoForm.classList.add("hidden");
        if (chatMainInterface) {
            chatMainInterface.classList.remove("hidden");
            chatMainInterface.classList.add("flex");
        }

        if (chatMessages && chatMessages.children.length === 0 && currentUserInfo) {
            appendMessage(
                "AI",
                `Chào **${escapeHtml(currentUserInfo.name)}**! 👋\nSmartTable có thể giúp gì cho bạn hôm nay?`
            );
        }
    }

    // Thêm tin nhắn
    function appendMessage(sender, text) {
        if (!chatMessages) return;

        const div = document.createElement("div");
        div.className = (sender === "Bạn" || sender === "user") ? "text-right" : "text-left";

        let formattedText = escapeHtml(text);

                    // Xử lý markdown ảnh
                    const imgRegex = /!\[(.*?)\]\((.*?)\)/g;

                    formattedText = formattedText.replace(imgRegex, function (match, alt, url) {
                        alt = (alt || '').replace(/"/g, '&quot;');   
                        url = (url || '#').trim();

                        return `
                        <br>
                        <a href="${url}" target="_blank" rel="noopener noreferrer">
                            <img src="${url}" alt="${alt}"                 
                        </a><br>`;
                                });

                    // XÓA luôn các dòng text dư kiểu: class="max-w-full h-auto..."
                    formattedText = formattedText.replace(
                        /(^|\n)\s*class="max-w-full[\s\S]*?onerror="this\.style\.display='none'"\s*\/?>/g,
                        ''
                    );


        // In đậm
        formattedText = formattedText.replace(/\*\*(.*?)\*\*/g, "<b>$1</b>");

        // Xuống dòng
        formattedText = formattedText.replace(/\n/g, "<br>");

        const safeSender = escapeHtml(
            sender === "ai" || sender === "AI" ? "Trợ lý ảo" : sender
        );

        div.innerHTML = `
            <div class="inline-block px-4 py-2 rounded-lg text-sm
                        ${sender === 'Bạn' || sender === 'user'
                ? 'bg-blue-600 text-white rounded-tr-none'
                : 'bg-gray-200 text-gray-800 rounded-tl-none'}">
                <strong>${safeSender}:</strong><br>
                ${formattedText}
            </div>
        `;
        chatMessages.appendChild(div);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    // Gửi tin nhắn
    async function sendMessage() {
        if (!chatInput || !chatSend) return;

        const msg = chatInput.value.trim();
        if (!msg) return;

        appendMessage("Bạn", msg);
        chatInput.value = "";

        const originalIcon = chatSend.innerHTML;
        chatSend.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
        chatSend.disabled = true;
        chatInput.disabled = true;

        try {
            const headers = { "Content-Type": "application/json" };
            const token = getAntiForgeryToken();
            if (token) headers["RequestVerificationToken"] = token;

            const payload = {
                message: msg,
                userName: currentUserInfo ? currentUserInfo.name : null,
                userPhone: currentUserInfo ? currentUserInfo.phone : null
            };

            const response = await fetch("/Chat/SendMessage", {
                method: "POST",
                headers,
                body: JSON.stringify(payload)
            });

            if (!response.ok) throw new Error("Lỗi server: " + response.status);

            const data = await response.json();
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

    if (chatSend) {
        chatSend.addEventListener("click", sendMessage);
    }

    if (chatInput) {
        chatInput.addEventListener("keypress", function (e) {
            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });
    }

    // ==========================
    // 4. NHÀ HÀNG GẦN BẠN
    // ==========================
    const nearbyBtnDesktop = document.getElementById("nearbyBtnDesktop");
    const nearbyBtnMobile = document.getElementById("nearbyBtnMobile");
    const nearbyModal = document.getElementById("nearbyModal");
    const nearbyModalClose = document.getElementById("nearbyModalClose");
    const nearbyList = document.getElementById("nearbyList");
    const mobileMenu = document.getElementById("mobileMenu");
    const mobileMenuButton = document.getElementById("mobileMenuButton");

    async function openNearbyModalAndSearch() {
        if (!nearbyModal || !nearbyList) return;

        nearbyModal.classList.remove("hidden");
        nearbyList.innerHTML =
            '<div class="flex flex-col items-center justify-center p-8 text-center">' +
            '<i class="fas fa-spinner fa-spin text-4xl text-blue-500"></i>' +
            '<p class="mt-3 text-gray-600">Đang xác định vị trí...</p>' +
            '</div>';

        if (!navigator.geolocation) {
            nearbyList.innerHTML =
                '<p class="text-yellow-600 text-center p-4">Trình duyệt của bạn không hỗ trợ định vị.</p>';
            return;
        }

        navigator.geolocation.getCurrentPosition(async function (position) {
            const nearbyStatusP = nearbyList.querySelector("p");
            if (nearbyStatusP) nearbyStatusP.textContent = "Đang tìm nhà hàng gần bạn...";

            const lat = position.coords.latitude;
            const lng = position.coords.longitude;

            try {
                const url = '/PublicRestaurant/GetNearbyMapData?lat=' +
                    encodeURIComponent(lat) +
                    '&lng=' + encodeURIComponent(lng) +
                    '&radiusKm=5';

                const response = await fetch(url);
                if (!response.ok) throw new Error("Lỗi mạng: " + response.status);

                const data = await response.json();
                nearbyList.innerHTML = "";

                if (!data || data.length === 0) {
                    nearbyList.innerHTML =
                        '<p class="text-gray-500 text-center p-4">Không tìm thấy nhà hàng nào trong bán kính 5km.</p>';
                    return;
                }

                data.forEach(function (r) {
                    const name = r.Name || r.name || "N/A";
                    const address = r.Address || r.address || "N/A";
                    const id = r.Id || r.restaurant_id || r.restaurantId || "";
                    const distVal = (r.Distance !== undefined && r.Distance !== null)
                        ? Number(r.Distance)
                        : (r.distanceKm !== undefined ? Number(r.distanceKm) : null);

                    const item = document.createElement("div");
                    item.className =
                        "p-3 border border-gray-200 rounded-lg hover:shadow-md transition-shadow cursor-pointer bg-white mb-2";

                    item.innerHTML = `
                        <div class="flex justify-between items-center mb-1">
                            <h4 class="text-base font-semibold text-blue-600">${escapeHtml(name)}</h4>
                            <span class="text-sm font-medium text-green-600">
                                ${distVal !== null ? distVal.toFixed(1) + " km" : ""}
                            </span>
                        </div>
                        <p class="text-xs text-gray-500 truncate">
                            <i class="fas fa-map-marker-alt mr-1"></i>${escapeHtml(address)}
                        </p>
                    `;

                    item.onclick = function () {
                        if (id) {
                            window.location.href = '/PublicRestaurant/Details/' + encodeURIComponent(id);
                        }
                    };

                    nearbyList.appendChild(item);
                });
            } catch (err) {
                nearbyList.innerHTML =
                    '<p class="text-red-500 text-center p-4">Lỗi kết nối.</p>';
                console.error(err);
            }
        }, function (err) {
            let message = "Vui lòng cho phép truy cập vị trí.";
            if (err && err.code === 1) {
                message = "Bạn đã từ chối quyền truy cập vị trí.";
            }
            nearbyList.innerHTML =
                '<p class="text-red-500 text-center p-4">' + escapeHtml(message) + '</p>';
        }, {
            enableHighAccuracy: true,
            timeout: 10000,
            maximumAge: 0
        });
    }

    if (nearbyBtnDesktop) {
        nearbyBtnDesktop.addEventListener("click", openNearbyModalAndSearch);
    }

    if (nearbyBtnMobile) {
        nearbyBtnMobile.addEventListener("click", function () {
            openNearbyModalAndSearch();
            if (mobileMenu) mobileMenu.classList.add("hidden");
            if (mobileMenuButton) {
                const icon = mobileMenuButton.querySelector("i");
                if (icon) {
                    icon.classList.remove("fa-times");
                    icon.classList.add("fa-bars");
                }
            }
        });
    }

    if (nearbyModalClose && nearbyModal) {
        nearbyModalClose.addEventListener("click", function () {
            nearbyModal.classList.add("hidden");
        });
    }

    if (nearbyModal) {
        nearbyModal.addEventListener("click", function (e) {
            if (e.target === nearbyModal) {
                nearbyModal.classList.add("hidden");
            }
        });
    }

    // ==========================
    // 5. MOBILE MENU
    // ==========================
    if (mobileMenuButton && mobileMenu) {
        mobileMenuButton.addEventListener("click", function () {
            mobileMenu.classList.toggle("hidden");
            const icon = mobileMenuButton.querySelector("i");
            if (icon) {
                icon.classList.toggle("fa-bars");
                icon.classList.toggle("fa-times");
            }
        });

        mobileMenu.querySelectorAll("a, button").forEach(function (item) {
            if (item.id !== "nearbyBtnMobile") {
                item.addEventListener("click", function () {
                    mobileMenu.classList.add("hidden");
                    const icon = mobileMenuButton.querySelector("i");
                    if (icon) {
                        icon.classList.remove("fa-times");
                        icon.classList.add("fa-bars");
                    }
                });
            }
        });
    }
});
