(function ($) {
	"use strict";

	var ignoreSel = "a, button, input, label, select, textarea, form, .btn, .admin-check-col, .menu-tree__handle, .menu-tree__actions";

	$(document).on("click", "tr[data-href], .js-admin-row-link[data-href]", function (e) {
		if ($(e.target).closest(ignoreSel).length) return;
		var href = this.getAttribute("data-href");
		if (!href) return;
		if (e.ctrlKey || e.metaKey || e.button === 1) {
			window.open(href, "_blank", "noopener");
			return;
		}
		window.location.href = href;
	});
})(jQuery);
