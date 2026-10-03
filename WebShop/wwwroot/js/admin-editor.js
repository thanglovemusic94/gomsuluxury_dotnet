window.WebShopEditor = (function () {
	var defaultConfig = {
		language: 'vi',
		height: 420,
		allowedContent: true,
		extraAllowedContent: '*(*);*{*}',
		entities: false,
		basicEntities: false,
		fillEmptyBlocks: false,
		versionCheck: false,
		removePlugins: 'exportpdf',
		filebrowserBrowseUrl: '/Admin/CkFinder/Browse?type=Files&size=medium&folder=',
		filebrowserImageBrowseUrl: '/Admin/CkFinder/Browse?type=Images&size=medium&folder=trang',
		filebrowserUploadUrl: '/Admin/CkFinder/Upload?type=Files&folder=trang&responseType=json',
		filebrowserImageUploadUrl: '/Admin/CkFinder/Upload?type=Images&folder=trang&responseType=json',
		toolbar: [
			{ name: 'document', items: ['Source', '-', 'Preview'] },
			{ name: 'clipboard', items: ['Cut', 'Copy', 'Paste', 'PasteText', 'PasteFromWord', '-', 'Undo', 'Redo'] },
			{ name: 'editing', items: ['Find', 'Replace', '-', 'SelectAll'] },
			{ name: 'basicstyles', items: ['Bold', 'Italic', 'Underline', 'Strike', 'Subscript', 'Superscript', '-', 'RemoveFormat'] },
			{ name: 'paragraph', items: ['NumberedList', 'BulletedList', '-', 'Outdent', 'Indent', '-', 'Blockquote', 'CreateDiv', '-', 'JustifyLeft', 'JustifyCenter', 'JustifyRight', 'JustifyBlock'] },
			{ name: 'links', items: ['Link', 'Unlink', 'Anchor'] },
			{ name: 'insert', items: ['Image', 'Table', 'HorizontalRule', 'SpecialChar', 'Iframe'] },
			{ name: 'styles', items: ['Styles', 'Format', 'Font', 'FontSize'] },
			{ name: 'colors', items: ['TextColor', 'BGColor'] },
			{ name: 'tools', items: ['Maximize', 'ShowBlocks'] }
		]
	};

	var rawProtectedSource = [
		/<script[\s\S]*?<\/script>/gi,
		/<style[\s\S]*?<\/style>/gi
	];

	function mergeConfig(extra) {
		var cfg = {};
		Object.keys(defaultConfig).forEach(function (key) {
			cfg[key] = defaultConfig[key];
		});
		if (!extra) return cfg;
		Object.keys(extra).forEach(function (key) {
			cfg[key] = extra[key];
		});
		return cfg;
	}

	function replace(selector, extraConfig) {
		if (!window.CKEDITOR) return [];
		var cfg = mergeConfig(extraConfig);
		var editors = [];
		document.querySelectorAll(selector).forEach(function (el) {
			if (!el.id)
				el.id = 'ckeditor_' + Math.random().toString(36).slice(2, 10);
			if (CKEDITOR.instances[el.id]) {
				editors.push(CKEDITOR.instances[el.id]);
				return;
			}
			editors.push(CKEDITOR.replace(el, cfg));
		});
		return editors;
	}

	function replaceBlock(selector) {
		return replace(selector, {
			height: 380,
			protectedSource: rawProtectedSource.slice(),
			extraAllowedContent: 'script(*){*};style(*){*};*(*);*{*}'
		});
	}

	function resolveTextarea(textareaSelector) {
		return document.querySelector(textareaSelector);
	}

	function resolveEditor(textareaSelector) {
		var el = resolveTextarea(textareaSelector);
		if (!el || !window.CKEDITOR) return null;
		if (el.id && CKEDITOR.instances[el.id])
			return CKEDITOR.instances[el.id];
		for (var name in CKEDITOR.instances) {
			if (!Object.prototype.hasOwnProperty.call(CKEDITOR.instances, name)) continue;
			var ed = CKEDITOR.instances[name];
			if (ed && ed.element && ed.element.$ === el)
				return ed;
		}
		return null;
	}

	function getEditorHtml(textareaSelector) {
		var editor = resolveEditor(textareaSelector);
		if (editor) {
			try { return editor.getData() || ''; } catch (e) { /* ignore */ }
		}
		var el = resolveTextarea(textareaSelector);
		return el ? (el.value || '') : '';
	}

	function buildPreviewDocument(html) {
		return '<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">'
			+ '<meta name="viewport" content="width=device-width, initial-scale=1">'
			+ '<base href="' + location.origin + '/">'
			+ '<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css">'
			+ '<link rel="stylesheet" href="/css/site.css">'
			+ '<link rel="stylesheet" href="/css/shop.css">'
			+ '<style>html,body{margin:0;padding:0;background:#fff;}body{padding:16px;min-height:100%;box-sizing:border-box;}</style>'
			+ '</head><body>' + (html || '<p style="color:#999">Chưa có nội dung để xem trước.</p>') + '</body></html>';
	}

	function writePreview(frame, html) {
		if (!frame) return;
		var docHtml = buildPreviewDocument(html);
		try {
			frame.removeAttribute('srcdoc');
			frame.srcdoc = docHtml;
		} catch (e1) { /* ignore */ }

		// Fallback for browsers / embeds where srcdoc is flaky
		try {
			var doc = frame.contentDocument || (frame.contentWindow && frame.contentWindow.document);
			if (doc) {
				doc.open();
				doc.write(docHtml);
				doc.close();
			}
		} catch (e2) { /* ignore */ }
	}

	function refreshPreview(textareaSelector, iframeSelector) {
		var frame = document.querySelector(iframeSelector);
		writePreview(frame, getEditorHtml(textareaSelector));
	}

	function bindBlockPreview(textareaSelector, iframeSelector, buttonSelector) {
		var attempts = 0;
		var run = function () { refreshPreview(textareaSelector, iframeSelector); };
		var bindEditor = function () {
			attempts += 1;
			var editor = resolveEditor(textareaSelector);
			if (!editor) {
				if (attempts < 40) setTimeout(bindEditor, 100);
				else run();
				return;
			}
			editor.on('instanceReady', run);
			editor.on('change', function () {
				clearTimeout(editor._previewTimer);
				editor._previewTimer = setTimeout(run, 350);
			});
			editor.on('mode', function () { setTimeout(run, 50); });
			editor.on('key', function () {
				clearTimeout(editor._previewTimer);
				editor._previewTimer = setTimeout(run, 450);
			});
			run();
		};

		// Immediate paint from textarea value (before CKEditor finishes)
		run();
		bindEditor();

		var btn = buttonSelector ? document.querySelector(buttonSelector) : null;
		if (btn) btn.addEventListener('click', function (e) { e.preventDefault(); run(); });
	}

	function bindForms() {
		document.querySelectorAll('form').forEach(function (form) {
			form.addEventListener('submit', function () {
				if (!window.CKEDITOR) return;
				for (var name in CKEDITOR.instances) {
					if (Object.prototype.hasOwnProperty.call(CKEDITOR.instances, name))
						CKEDITOR.instances[name].updateElement();
				}
			});
		});
	}

	function bindMediaPickers() {
		document.querySelectorAll('.js-media-pick').forEach(function (btn) {
			btn.addEventListener('click', function () {
				var inputId = btn.getAttribute('data-input') || '';
				var size = btn.getAttribute('data-size') || 'medium';
				var folder = btn.getAttribute('data-folder') || '';
				var url = '/Admin/CkFinder/Browse?type=Images'
					+ '&size=' + encodeURIComponent(size)
					+ '&Folder=' + encodeURIComponent(folder)
					+ '&InputId=' + encodeURIComponent(inputId);
				window.open(url, 'webshop_media', 'width=1100,height=720,scrollbars=yes');
			});
		});
	}

	return {
		replace: replace,
		replaceBlock: replaceBlock,
		bindForms: bindForms,
		bindMediaPickers: bindMediaPickers,
		bindBlockPreview: bindBlockPreview,
		refreshPreview: refreshPreview,
		writePreview: writePreview,
		buildPreviewDocument: buildPreviewDocument
	};
})();
