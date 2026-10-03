(function ($) {
	"use strict";

	function canHardDelete() {
		return document.body.getAttribute("data-admin-can-hard-delete") === "1";
	}

	function setMode($form, mode) {
		var $field = $form.find('input[name="mode"]');
		if ($field.length === 0) {
			$field = $('<input type="hidden" name="mode" />').appendTo($form);
		}
		$field.val(mode);
	}

	function openDeleteModal(count, noun, onPick) {
		var $modal = $("#admin-delete-modal");
		if ($modal.length === 0) {
			var mode = window.confirm("Chuyển " + count + " " + noun + " vào thùng rác?") ? "trash" : null;
			if (mode) onPick(mode);
			return;
		}

		var label = count === 1 ? ("1 " + noun) : (count + " " + noun);
		$modal.find(".js-admin-delete-count").text(label);
		$modal.find(".js-admin-delete-hard").toggle(canHardDelete());
		$modal.data("pick", onPick);
		$modal.modal("show");
	}

	$(document).on("click", "#admin-delete-modal [data-delete-mode]", function () {
		var mode = $(this).attr("data-delete-mode");
		var $modal = $("#admin-delete-modal");
		var pick = $modal.data("pick");
		$modal.modal("hide");
		if (typeof pick === "function") pick(mode);
	});

	$(document).on("submit", "form.js-admin-delete-one", function (e) {
		var $form = $(this);
		if ($form.data("confirmed")) return true;
		e.preventDefault();

		var noun = $form.attr("data-noun") || "mục";
		if (!canHardDelete()) {
			if (!window.confirm("Chuyển " + noun + " vào thùng rác?")) return false;
			setMode($form, "trash");
			$form.data("confirmed", true);
			$form.trigger("submit");
			return false;
		}

		openDeleteModal(1, noun, function (mode) {
			setMode($form, mode);
			$form.data("confirmed", true);
			$form.trigger("submit");
		});
		return false;
	});

	function refreshBulk($root) {
		var $boxes = $root.find(".js-admin-row-check");
		var checked = $boxes.filter(":checked").length;
		$root.find(".js-admin-bulk-count").text(checked);
		$root.find(".js-admin-bulk-delete").prop("disabled", checked === 0);
		var $all = $root.find(".js-admin-select-all");
		if ($boxes.length === 0) {
			$all.prop("checked", false).prop("indeterminate", false);
		} else {
			$all.prop("checked", checked === $boxes.length);
			$all.prop("indeterminate", checked > 0 && checked < $boxes.length);
		}
	}

	$(document).on("change", ".js-admin-select-all", function () {
		var $root = $(this).closest(".js-admin-bulk-root");
		$root.find(".js-admin-row-check").prop("checked", this.checked);
		refreshBulk($root);
	});

	$(document).on("change", ".js-admin-row-check", function () {
		refreshBulk($(this).closest(".js-admin-bulk-root"));
	});

	$(document).on("submit", "form.js-admin-bulk-form", function (e) {
		var $form = $(this);
		if ($form.data("confirmed")) return true;
		e.preventDefault();

		var $root = $form.closest(".js-admin-bulk-root");
		var checked = $root.find(".js-admin-row-check:checked").length;
		if (checked === 0) {
			window.alert("Chọn ít nhất một mục.");
			return false;
		}

		var noun = $form.attr("data-noun") || "mục";
		if (!canHardDelete()) {
			if (!window.confirm("Chuyển " + checked + " " + noun + " vào thùng rác?")) return false;
			setMode($form, "trash");
			$form.data("confirmed", true);
			$form.trigger("submit");
			return false;
		}

		openDeleteModal(checked, noun, function (mode) {
			setMode($form, mode);
			$form.data("confirmed", true);
			$form.trigger("submit");
		});
		return false;
	});

	$(function () {
		$(".js-admin-bulk-root").each(function () {
			refreshBulk($(this));
		});
	});
})(jQuery);
