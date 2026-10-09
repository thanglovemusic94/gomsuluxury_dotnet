(function () {
	"use strict";

	var root = document.getElementById("menu-tree-root");
	if (!root || typeof Sortable === "undefined") return;

	var statusEl = document.getElementById("menu-tree-status");
	var tokenInput = document.querySelector("#menu-reorder-af input[name='gomsu_token']");
	var saveTimer = null;
	var sortables = [];

	function showStatus(text, isError) {
		if (!statusEl) return;
		statusEl.hidden = false;
		statusEl.textContent = text;
		statusEl.className = "menu-tree-status" + (isError ? " is-error" : " is-ok");
	}

	function collect(parentUl, parentId, out) {
		var items = parentUl.querySelectorAll(":scope > .menu-tree__item");
		items.forEach(function (li, index) {
			var id = parseInt(li.getAttribute("data-id"), 10);
			if (!id) return;
			out.push({ id: id, parentId: parentId, displayOrder: index });
			var childUl = li.querySelector(":scope > .menu-tree__children");
			if (childUl) collect(childUl, id, out);
		});
	}

	function save() {
		var payload = [];
		collect(root, null, payload);
		showStatus("Đang lưu…", false);

		fetch(window.location.pathname + "?handler=Reorder", {
			method: "POST",
			headers: {
				"Content-Type": "application/json",
				"X-Gomsu-Token": tokenInput ? tokenInput.value : ""
			},
			body: JSON.stringify(payload),
			credentials: "same-origin"
		})
			.then(function (res) { return res.json().then(function (data) { return { ok: res.ok, data: data }; }); })
			.then(function (result) {
				if (result.data && result.data.ok) {
					showStatus("Đã lưu cấu trúc menu.", false);
				} else {
					showStatus((result.data && result.data.error) || "Không lưu được.", true);
				}
			})
			.catch(function () {
				showStatus("Lỗi mạng khi lưu menu.", true);
			});
	}

	function scheduleSave() {
		clearTimeout(saveTimer);
		saveTimer = setTimeout(save, 280);
	}

	function bindSortable(ul) {
		var existing = sortables.find(function (s) { return s.el === ul; });
		if (existing) return;

		var instance = Sortable.create(ul, {
			group: "admin-menus",
			animation: 150,
			handle: ".menu-tree__handle",
			draggable: ".menu-tree__item",
			fallbackOnBody: true,
			swapThreshold: 0.65,
			emptyInsertThreshold: 12,
			onAdd: function () {
				ensureChildLists();
			},
			onEnd: function () {
				ensureChildLists();
				scheduleSave();
			}
		});
		sortables.push(instance);
	}

	function ensureChildLists() {
		root.querySelectorAll(".menu-tree__item").forEach(function (li) {
			var childUl = li.querySelector(":scope > .menu-tree__children");
			if (!childUl) {
				childUl = document.createElement("ul");
				childUl.className = "menu-tree__children";
				li.appendChild(childUl);
			}
			bindSortable(childUl);
		});
		bindSortable(root);
	}

	ensureChildLists();
})();
