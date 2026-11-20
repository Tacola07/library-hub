// LIBRARY HUB — JS chỉ lo tương tác giao diện.
// Dữ liệu và Thêm/Sửa/Xóa do Controller + SQL Server (backend) xử lý.

// 1) Toast: hiện thông báo nếu backend gán TempData["Toast"]
function toast(msg) {
  const t = document.getElementById("toast");
  if (!t || !msg) return;
  t.textContent = msg;
  t.style.display = "block";
  setTimeout(() => (t.style.display = "none"), 2400);
}
document.addEventListener("DOMContentLoaded", () => {
  const t = document.getElementById("toast");
  if (t && t.dataset.message) toast(t.dataset.message);
});

// 2) Ctrl + K: đưa con trỏ vào ô tìm kiếm chung
document.addEventListener("keydown", (e) => {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
    e.preventDefault();
    document.getElementById("globalSearch")?.focus();
  }
});

// 3) Hỏi xác nhận trước khi gửi form có data-confirm (VD: nút Xóa)
document.addEventListener("submit", (e) => {
  const msg = e.target.dataset.confirm;
  if (msg && !confirm(msg)) e.preventDefault();
});

// 4) Lọc nhanh bảng ngay khi gõ (input có data-filter="#idBang").
//    Nhấn Enter vẫn gửi ?q=... lên server để tìm thật.
document.querySelectorAll("input[data-filter]").forEach((input) => {
  const table = document.querySelector(input.dataset.filter);
  if (!table) return;
  const run = () => {
    const q = input.value.trim().toLowerCase();
    table.querySelectorAll("tbody tr").forEach((tr) => {
      tr.style.display = tr.textContent.toLowerCase().includes(q) ? "" : "none";
    });
  };
  input.addEventListener("input", run);
  run();
});

// 5) Chuông: bấm mở cửa sổ nhỏ tin tức & khuyến mãi. Chỉ gọi server khi mở lần đầu trên mỗi trang.
(function () {
  const btn = document.getElementById("bellBtn");
  const panel = document.getElementById("bellPanel");
  const body = document.getElementById("bellBody");
  if (!btn || !panel || !body) return;

  try { if (sessionStorage.getItem("bellSeen")) btn.classList.add("seen"); } catch (_) {}

  let loaded = false;
  const close = () => { panel.hidden = true; btn.setAttribute("aria-expanded", "false"); };

  btn.addEventListener("click", async (e) => {
    e.stopPropagation();
    const opening = panel.hidden;
    panel.hidden = !opening;
    btn.setAttribute("aria-expanded", String(opening));
    if (!opening) return;

    btn.classList.add("seen"); // tắt chấm đỏ sau khi đã xem
    try { sessionStorage.setItem("bellSeen", "1"); } catch (_) {}

    if (loaded) return;
    try {
      const res = await fetch(btn.dataset.url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
      if (!res.ok) throw new Error(res.status);
      body.innerHTML = await res.text(); // HTML do server (Razor) render và đã mã hóa
      loaded = true;
    } catch (_) {
      body.innerHTML = '<p class="muted bell-empty">Không tải được tin. Vui lòng thử lại.</p>';
    }
  });
  document.addEventListener("click", (e) => {
    if (!panel.hidden && !panel.contains(e.target) && !btn.contains(e.target)) close();
  });
  document.addEventListener("keydown", (e) => { if (e.key === "Escape") close(); });
})();

// 6) Khách chưa đăng nhập: bấm vào chức năng cần tài khoản (link có data-login-required)
//    => hiện popup hỏi "Đăng nhập" hay "Tiếp tục xem ẩn danh" thay vì tự chuyển trang.
//    Popup cũng được server mở sẵn khi khách gõ thẳng URL trang cần đăng nhập (?needLogin=1).
(function () {
  const modal = document.getElementById("loginModal");
  if (!modal) return;
  const go = document.getElementById("loginModalGo");

  const open = (returnUrl) => {
    if (go && returnUrl) go.href = go.dataset.base + "?returnUrl=" + encodeURIComponent(returnUrl);
    modal.hidden = false;
    go?.focus();
  };
  const close = () => {
    modal.hidden = true;
    const url = new URL(location.href);
    if (url.searchParams.has("needLogin")) { // dọn tham số để F5 không hiện lại popup
      url.searchParams.delete("needLogin");
      url.searchParams.delete("returnUrl");
      history.replaceState(null, "", url.pathname + url.search + url.hash);
    }
  };

  document.addEventListener("click", (e) => {
    const link = e.target.closest("a[data-login-required]");
    if (link) { e.preventDefault(); open(link.pathname + link.search); return; }
    if (e.target.closest("[data-modal-close]")) { e.preventDefault(); close(); return; }
    if (e.target === modal) close(); // bấm nền tối
  });
  document.addEventListener("keydown", (e) => { if (e.key === "Escape" && !modal.hidden) close(); });
  if (!modal.hidden) go?.focus();
})();

// 7) Ô chọn có data-autosubmit: đổi lựa chọn là gửi form ngay (bộ lọc sách).
//    data-reset="#id": xóa lựa chọn của ô phụ thuộc (đổi thể loại thì bỏ chọn tác giả cũ).
document.querySelectorAll("select[data-autosubmit]").forEach((sel) => {
  sel.addEventListener("change", () => {
    if (sel.dataset.reset) {
      const dep = document.querySelector(sel.dataset.reset);
      if (dep) dep.value = "";
    }
    sel.form?.submit();
  });
});

// 8) Thêm / xóa dòng nhập (chỉnh sửa hồ sơ). Dòng mới lấy từ <template>, "__i__" thay bằng chỉ số duy nhất
//    (ASP.NET đọc được chỉ số không liên tục nhờ trường ẩn "Infos.index").
let rowSeq = Date.now();
document.addEventListener("click", (e) => {
  const add = e.target.closest("[data-add-row]");
  if (add) {
    const host = document.querySelector(add.dataset.addRow);
    const tpl = document.querySelector(add.dataset.template);
    if (host && tpl) {
      host.insertAdjacentHTML("beforeend", tpl.innerHTML.replaceAll("__i__", String(rowSeq++)));
      host.lastElementChild?.querySelector("input[type=text]")?.focus();
    }
    return;
  }
  const rm = e.target.closest("[data-remove-row]");
  if (rm) rm.closest(".info-row")?.remove();
});
