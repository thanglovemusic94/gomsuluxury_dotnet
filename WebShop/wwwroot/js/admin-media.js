(function () {
	"use strict";

	var toastEl = document.getElementById("mediaToast");
	if (!toastEl) return;

	var workspace = document.getElementById("mediaWorkspace");
	var drawer = document.getElementById("mediaDrawer");
	var seoModal = document.getElementById("mediaSeoModal");
	var seoRows = document.getElementById("mediaSeoRows");
	var seoStatus = document.getElementById("mediaSeoStatus");
	var seoBtn = document.getElementById("mediaSeoBtn");
	var selectPage = document.getElementById("mediaSelectPage");
	var selectedCount = document.getElementById("mediaSelectedCount");
	var bulkFolderIds = document.getElementById("mediaBulkFolderIds");
	var bulkDeleteIds = document.getElementById("mediaBulkDeleteIds");
	var bulkFolderBtn = document.getElementById("mediaBulkFolderBtn");
	var bulkDeleteBtn = document.getElementById("mediaBulkDeleteBtn");
	var drop = document.getElementById("mediaDrop");
	var fileInput = document.getElementById("mediaFileInput");
	var uploadProgress = document.getElementById("mediaUploadProgress");
	var uploadBar = document.getElementById("mediaUploadBar");
	var uploadText = document.getElementById("mediaUploadText");
	var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
	var token = tokenInput ? tokenInput.value : "";
	var toastTimer;
	var activeCard = null;

	function showToast(html) {
		toastEl.innerHTML = html;
		toastEl.classList.add("is-show");
		clearTimeout(toastTimer);
		toastTimer = setTimeout(function () { toastEl.classList.remove("is-show"); }, 2400);
	}

	function esc(s) {
		return String(s == null ? "" : s)
			.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
			.replace(/"/g, "&quot;");
	}

	function copyText(url, label, btn) {
		if (!url) {
			showToast("Không có URL để copy.");
			return;
		}
		var done = function () {
			if (btn) {
				btn.classList.add("is-copied");
				setTimeout(function () { btn.classList.remove("is-copied"); }, 900);
			}
			showToast("Đã copy <strong>" + esc(label || "URL") + "</strong><br><code>" + esc(url) + "</code>");
		};
		if (navigator.clipboard && navigator.clipboard.writeText) {
			navigator.clipboard.writeText(url).then(done).catch(function () {
				fallbackCopy(url);
				done();
			});
		} else {
			fallbackCopy(url);
			done();
		}
	}

	function fallbackCopy(url) {
		var t = document.createElement("input");
		t.value = url;
		document.body.appendChild(t);
		t.select();
		document.execCommand("copy");
		document.body.removeChild(t);
	}

	function selectedIds() {
		return Array.prototype.map.call(document.querySelectorAll(".js-media-pick:checked"), function (box) {
			return box.value;
		});
	}

	function fillIdInputs(container, ids) {
		if (!container) return;
		container.innerHTML = "";
		ids.forEach(function (id) {
			var input = document.createElement("input");
			input.type = "hidden";
			input.name = "ids";
			input.value = id;
			container.appendChild(input);
		});
	}

	function syncSelection() {
		var boxes = document.querySelectorAll(".js-media-pick");
		var checked = document.querySelectorAll(".js-media-pick:checked");
		var n = checked.length;
		if (selectedCount) selectedCount.textContent = n + " đã chọn";
		if (seoBtn) seoBtn.disabled = n === 0;
		if (bulkFolderBtn) bulkFolderBtn.disabled = n === 0;
		if (bulkDeleteBtn) bulkDeleteBtn.disabled = n === 0;
		if (selectPage) selectPage.checked = boxes.length > 0 && checked.length === boxes.length;
		var ids = selectedIds();
		fillIdInputs(bulkFolderIds, ids);
		fillIdInputs(bulkDeleteIds, ids);
		boxes.forEach(function (box) {
			var card = box.closest(".media-card");
			if (card) card.classList.toggle("is-selected", box.checked);
		});
	}

	function closeDrawer() {
		if (workspace) workspace.classList.remove("has-drawer");
		if (drawer) drawer.setAttribute("aria-hidden", "true");
		if (activeCard) activeCard.classList.remove("is-active");
		activeCard = null;
	}

	function openDrawer(card) {
		if (!workspace || !drawer || !card) return;
		if (activeCard) activeCard.classList.remove("is-active");
		activeCard = card;
		card.classList.add("is-active");
		workspace.classList.add("has-drawer");
		drawer.setAttribute("aria-hidden", "false");

		var id = card.getAttribute("data-id") || "0";
		var name = card.getAttribute("data-name") || "";
		var alt = card.getAttribute("data-alt") || "";
		var folder = card.getAttribute("data-folder") || "khac";
		var folderLabel = card.getAttribute("data-folder-label") || "";
		var size = card.getAttribute("data-size") || "";
		var usage = parseInt(card.getAttribute("data-usage") || "0", 10);
		var isImage = card.getAttribute("data-image") === "1";
		var preview = card.getAttribute("data-preview") || card.getAttribute("data-medium") || card.getAttribute("data-original") || "";
		var thumb = card.getAttribute("data-thumb") || "";
		var medium = card.getAttribute("data-medium") || "";
		var large = card.getAttribute("data-large") || "";
		var original = card.getAttribute("data-original") || "";
		var otf = card.getAttribute("data-otf") || "";

		document.getElementById("drawerTitle").textContent = alt || name || "Chi tiết";
		var previewEl = document.getElementById("drawerPreview");
		if (isImage && preview) {
			previewEl.innerHTML = '<img src="' + esc(large || preview) + '" alt="' + esc(alt || name) + '">';
		} else {
			previewEl.innerHTML = '<span class="text-muted">' + esc(name) + "</span>";
		}

		var sizes = document.getElementById("drawerSizes");
		sizes.innerHTML = "";
		function addSize(label, url) {
			if (!url) return;
			var btn = document.createElement("button");
			btn.type = "button";
			btn.className = "btn btn-default btn-sm";
			btn.textContent = label;
			btn.title = url;
			btn.addEventListener("click", function () { copyText(url, label, btn); });
			sizes.appendChild(btn);
		}
		if (isImage) {
			addSize("Thumb", thumb);
			addSize("Medium", medium);
			addSize("Large", large);
			addSize("Gốc", original);
			addSize("OTF 800", otf);
		} else {
			addSize("URL", original);
		}

		var oldMeta = drawer.querySelector(".js-drawer-meta");
		if (oldMeta) oldMeta.remove();
		var meta = document.createElement("div");
		meta.className = "text-muted js-drawer-meta";
		meta.style.fontSize = "11px";
		meta.style.marginBottom = "10px";
		meta.textContent = folderLabel + " · " + size + (usage > 0 ? " · đang dùng: " + usage : " · chưa dùng");
		sizes.parentNode.insertBefore(meta, sizes);

		document.getElementById("drawerAltId").value = id;
		document.getElementById("drawerAltInput").value = alt;
		document.getElementById("drawerFolderId").value = id;
		document.getElementById("drawerFolderSelect").value = folder;
		document.getElementById("drawerRegenId").value = id;
		document.getElementById("drawerDeleteId").value = id;
		document.getElementById("drawerDeleteName").value = name;
		document.getElementById("drawerDeleteForm").setAttribute(
			"data-confirm",
			usage > 0
				? ("Ảnh đang được dùng tại " + usage + " chỗ. Vẫn chuyển vào thùng rác?")
				: "Chuyển ảnh vào thùng rác?"
		);

		var regenForm = document.getElementById("drawerRegenForm");
		if (regenForm) regenForm.style.display = isImage && id !== "0" ? "" : "none";
		var altForm = document.getElementById("drawerAltForm");
		var folderForm = document.getElementById("drawerFolderForm");
		var deleteForm = document.getElementById("drawerDeleteForm");
		var usageBtn = document.getElementById("drawerUsageBtn");
		if (id === "0") {
			if (altForm) altForm.style.display = "none";
			if (folderForm) folderForm.style.display = "none";
			if (deleteForm) deleteForm.style.display = "none";
			if (usageBtn) usageBtn.style.display = "none";
		} else {
			if (altForm) altForm.style.display = "";
			if (folderForm) folderForm.style.display = "";
			if (deleteForm) deleteForm.style.display = "";
			if (usageBtn) usageBtn.style.display = "";
		}

		var usagePanel = document.getElementById("drawerUsage");
		usagePanel.classList.remove("is-open");
		usagePanel.innerHTML = "";
		usageBtn.setAttribute("data-loaded", "0");
		usageBtn.setAttribute("data-id", id);
	}

	document.querySelectorAll(".media-card").forEach(function (card) {
		card.addEventListener("click", function (e) {
			if (e.target.closest(".js-media-pick")) return;
			openDrawer(card);
		});
	});

	document.querySelectorAll(".js-media-pick").forEach(function (box) {
		box.addEventListener("click", function (e) { e.stopPropagation(); });
		box.addEventListener("change", syncSelection);
	});

	if (selectPage) {
		selectPage.addEventListener("change", function () {
			document.querySelectorAll(".js-media-pick").forEach(function (box) {
				box.checked = selectPage.checked;
			});
			syncSelection();
		});
	}

	var closeBtn = document.getElementById("mediaDrawerClose");
	if (closeBtn) closeBtn.addEventListener("click", closeDrawer);

	var usageBtn = document.getElementById("drawerUsageBtn");
	if (usageBtn) {
		usageBtn.addEventListener("click", function () {
			var id = usageBtn.getAttribute("data-id");
			var panel = document.getElementById("drawerUsage");
			if (!panel || !id) return;
			if (panel.classList.contains("is-open") && usageBtn.getAttribute("data-loaded") === "1") {
				panel.classList.remove("is-open");
				return;
			}
			panel.classList.add("is-open");
			if (usageBtn.getAttribute("data-loaded") === "1") return;
			panel.innerHTML = "Đang tải…";
			fetch("/Admin/Media?handler=Usage&id=" + encodeURIComponent(id), {
				headers: { Accept: "application/json", RequestVerificationToken: token }
			})
				.then(function (r) { return r.json(); })
				.then(function (items) {
					usageBtn.setAttribute("data-loaded", "1");
					if (!items || !items.length) {
						panel.innerHTML = "Không thấy tham chiếu trong nội dung.";
						return;
					}
					var html = "<strong>" + items.length + " chỗ:</strong><ul>";
					items.forEach(function (u) {
						var label = (u.kind || "") + ": " + (u.label || "");
						html += u.link
							? "<li><a href=\"" + esc(u.link) + "\">" + esc(label) + "</a></li>"
							: "<li>" + esc(label) + "</li>";
					});
					html += "</ul>";
					panel.innerHTML = html;
				})
				.catch(function () {
					panel.innerHTML = "Không tải được danh sách sử dụng.";
				});
		});
	}

	/* Upload drag-drop */
	function uploadFiles(files) {
		if (!files || !files.length) return;
		var folderEl = document.getElementById("mediaUploadFolder");
		var altEl = document.getElementById("mediaUploadAlt");
		var folder = folderEl ? folderEl.value : "khac";
		var alt = altEl ? altEl.value : "";
		var list = Array.prototype.slice.call(files);
		var done = 0;
		var okCount = 0;
		var failCount = 0;
		if (uploadProgress) uploadProgress.classList.add("is-on");
		if (uploadBar) uploadBar.style.width = "0%";
		if (uploadText) uploadText.textContent = "Đang tải 0/" + list.length + "…";

		function next() {
			if (done >= list.length) {
				if (uploadText) {
					uploadText.textContent = "Xong: " + okCount + " thành công" + (failCount ? (", " + failCount + " lỗi") : "");
				}
				if (uploadBar) uploadBar.style.width = "100%";
				setTimeout(function () { window.location.reload(); }, 700);
				return;
			}
			var file = list[done];
			var body = new FormData();
			body.append("__RequestVerificationToken", token);
			body.append("file", file);
			body.append("folder", folder);
			body.append("alt", alt);
			body.append("Type", "Images");
			fetch("/Admin/Media?handler=UploadAjax", {
				method: "POST",
				headers: { RequestVerificationToken: token },
				body: body
			})
				.then(function (r) { return r.json(); })
				.then(function (res) {
					if (res && res.ok) okCount++;
					else failCount++;
					if (res && !res.ok && res.error) showToast(esc(res.error));
				})
				.catch(function () { failCount++; })
				.finally(function () {
					done++;
					var pct = Math.round((done / list.length) * 100);
					if (uploadBar) uploadBar.style.width = pct + "%";
					if (uploadText) uploadText.textContent = "Đang tải " + done + "/" + list.length + "…";
					next();
				});
		}
		next();
	}

	if (drop && fileInput) {
		drop.addEventListener("click", function () { fileInput.click(); });
		fileInput.addEventListener("change", function () {
			uploadFiles(fileInput.files);
			fileInput.value = "";
		});
		["dragenter", "dragover"].forEach(function (ev) {
			drop.addEventListener(ev, function (e) {
				e.preventDefault();
				e.stopPropagation();
				drop.classList.add("is-drag");
			});
		});
		["dragleave", "drop"].forEach(function (ev) {
			drop.addEventListener(ev, function (e) {
				e.preventDefault();
				e.stopPropagation();
				drop.classList.remove("is-drag");
			});
		});
		drop.addEventListener("drop", function (e) {
			uploadFiles(e.dataTransfer.files);
		});
	}

	/* SEO modal (kept) */
	function closeSeo() {
		if (!seoModal) return;
		seoModal.classList.remove("is-open");
		seoModal.setAttribute("aria-hidden", "true");
	}

	document.querySelectorAll(".js-seo-close").forEach(function (btn) {
		btn.addEventListener("click", closeSeo);
	});
	if (seoModal) {
		seoModal.addEventListener("click", function (e) {
			if (e.target === seoModal) closeSeo();
		});
	}

	document.addEventListener("keydown", function (e) {
		if (e.key === "Escape") {
			if (seoModal && seoModal.classList.contains("is-open")) closeSeo();
			else closeDrawer();
		}
	});

	function baseFromFileName(name) {
		var n = String(name || "");
		n = n.replace(/\.[^.]+$/, "");
		n = n.replace(/-\d{14}$/, "");
		return n;
	}

	function syncSeoSelectAll() {
		if (!seoRows) return;
		var boxes = seoRows.querySelectorAll(".js-seo-row");
		var checked = seoRows.querySelectorAll(".js-seo-row:checked");
		var all = document.getElementById("mediaSeoSelectAll");
		if (!all) return;
		all.checked = boxes.length > 0 && checked.length === boxes.length;
		all.indeterminate = checked.length > 0 && checked.length < boxes.length;
		if (seoStatus) seoStatus.textContent = checked.length + "/" + boxes.length + " ảnh sẽ áp dụng";
	}

	if (seoBtn) {
		seoBtn.addEventListener("click", function () {
			var ids = selectedIds();
			if (!ids.length) return;
			seoBtn.disabled = true;
			if (seoStatus) seoStatus.textContent = "Đang phân tích...";
			var body = new URLSearchParams();
			body.append("__RequestVerificationToken", token);
			ids.forEach(function (id) { body.append("ids", id); });
			fetch("/Admin/Media?handler=PreviewSeo", {
				method: "POST",
				headers: { RequestVerificationToken: token },
				body: body
			}).then(function (r) { return r.json(); }).then(function (rows) {
				seoRows.innerHTML = "";
				if (!rows || !rows.length) {
					if (seoStatus) seoStatus.textContent = "Không có ảnh hợp lệ để tối ưu.";
					seoBtn.disabled = false;
					return;
				}
				rows.forEach(function (row) {
					var tr = document.createElement("tr");
					var willRename = row.willRename ?? row.WillRename;
					var willUpdateAlt = row.willUpdateAlt ?? row.WillUpdateAlt;
					var newName = row.newName || row.NewName || "";
					var oldName = row.oldName || row.OldName || "";
					var newAlt = row.newAlt || row.NewAlt || "";
					var oldAlt = row.oldAlt || row.OldAlt || "";
					var thumbUrl = row.thumbUrl || row.ThumbUrl || "";
					var source = row.source || row.Source || "";
					var note = row.note || row.Note || "";
					var id = row.id || row.Id;
					var checked = willRename || willUpdateAlt ? " checked" : "";
					var base = baseFromFileName(newName);
					tr.innerHTML =
						'<td><input type="checkbox" class="js-seo-row"' + checked +
						' data-id="' + id + '"></td>' +
						'<td><img src="' + esc(thumbUrl) + '" alt=""></td>' +
						'<td><div class="text-muted">' + esc(oldName) + "</div>" +
						'<div><span class="media-seo-arrow">→</span> ' +
						'<input type="text" class="form-control input-sm js-seo-base" value="' + esc(base) + '" maxlength="60"></div>' +
						(note ? '<small class="text-muted">' + esc(note) + "</small>" : "") +
						"</td>" +
						'<td><input type="text" class="form-control input-sm js-seo-alt" value="' + esc(newAlt) + '" maxlength="255">' +
						'<small class="text-muted">Alt cũ: ' + esc(oldAlt || "(trống)") + "</small></td>" +
						'<td><span class="label label-info">' + esc(source) + "</span></td>";
					seoRows.appendChild(tr);
				});
				seoRows.querySelectorAll(".js-seo-row").forEach(function (box) {
					box.addEventListener("change", syncSeoSelectAll);
				});
				syncSeoSelectAll();
				if (seoStatus) seoStatus.textContent = rows.length + " ảnh · tick header để chọn/bỏ tất cả";
				seoModal.classList.add("is-open");
				seoModal.setAttribute("aria-hidden", "false");
			}).catch(function () {
				showToast("Không xem trước được. Thử lại.");
			}).finally(function () {
				syncSelection();
			});
		});
	}

	var seoSelectAll = document.getElementById("mediaSeoSelectAll");
	if (seoSelectAll) {
		seoSelectAll.addEventListener("change", function () {
			var on = this.checked;
			seoRows.querySelectorAll(".js-seo-row").forEach(function (box) { box.checked = on; });
			this.indeterminate = false;
			syncSeoSelectAll();
		});
	}

	var seoApply = document.getElementById("mediaSeoApply");
	if (seoApply) {
		seoApply.addEventListener("click", function () {
			var applyBtn = this;
			var payload = [];
			seoRows.querySelectorAll("tr").forEach(function (tr) {
				var box = tr.querySelector(".js-seo-row");
				if (!box || !box.checked) return;
				payload.push({
					id: parseInt(box.getAttribute("data-id"), 10),
					newBaseName: (tr.querySelector(".js-seo-base").value || "").trim(),
					newAlt: (tr.querySelector(".js-seo-alt").value || "").trim()
				});
			});
			if (!payload.length) {
				showToast("Chưa chọn ảnh nào trong bảng xem trước.");
				return;
			}
			applyBtn.disabled = true;
			if (seoStatus) seoStatus.textContent = "Đang áp dụng...";
			var body = new URLSearchParams();
			body.append("__RequestVerificationToken", token);
			body.append("payload", JSON.stringify(payload));
			fetch("/Admin/Media?handler=ApplySeo", {
				method: "POST",
				headers: { RequestVerificationToken: token },
				body: body
			}).then(function (r) { return r.json(); }).then(function (result) {
				showToast(esc(result.message || "Đã xong."));
				closeSeo();
				setTimeout(function () { window.location.reload(); }, 700);
			}).catch(function () {
				showToast("Áp dụng thất bại. Thử lại.");
			}).finally(function () {
				applyBtn.disabled = false;
			});
		});
	}

	syncSelection();
})();
