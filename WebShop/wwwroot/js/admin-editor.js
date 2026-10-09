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

	function antiforgeryToken() {
		var el = document.querySelector('input[name="gomsu_token"]');
		return el ? el.value : '';
	}

	function uploadImageFile(file, folder, onOk, onErr) {
		if (!file || (file.type || '').indexOf('image/') !== 0) {
			if (onErr) onErr('Chỉ nhận ảnh từ clipboard / kéo thả.');
			return;
		}
		var token = antiforgeryToken();
		var body = new FormData();
		var name = file.name && file.name !== 'image.png'
			? file.name
			: ('paste-' + Date.now() + '.png');
		body.append('gomsu_token', token);
		body.append('file', file, name);
		body.append('folder', folder || 'san-pham');
		body.append('alt', '');
		body.append('Type', 'Images');
		fetch('/Admin/Media?handler=UploadAjax', {
			method: 'POST',
			headers: { "X-Gomsu-Token": token },
			body: body
		})
			.then(function (r) { return r.json(); })
			.then(function (res) {
				if (res && res.ok && res.url) onOk(res.url);
				else if (onErr) onErr((res && res.error) || 'Upload thất bại.');
			})
			.catch(function () {
				if (onErr) onErr('Không tải được ảnh.');
			});
	}

	/** Paste/kéo ảnh vào `.js-media-paste[data-input]` → upload Media → điền URL. */
	function bindMediaPasteTargets() {
		document.querySelectorAll('.js-media-paste').forEach(function (zone) {
			if (zone.getAttribute('data-bound') === '1') return;
			zone.setAttribute('data-bound', '1');
			var inputId = zone.getAttribute('data-input') || '';
			var folder = zone.getAttribute('data-folder') || 'san-pham';
			var input = inputId ? document.getElementById(inputId) : null;
			if (!input) return;
			var status = zone.querySelector('.js-media-paste-status');
			var preview = zone.querySelector('.js-media-paste-preview');

			function setStatus(text) {
				if (status) status.textContent = text || '';
			}

			function applyUrl(url) {
				input.value = url;
				input.dispatchEvent(new Event('input', { bubbles: true }));
				input.dispatchEvent(new Event('change', { bubbles: true }));
				if (preview) {
					preview.src = url;
					preview.hidden = false;
				}
				setStatus('Đã dán ảnh vào thư viện.');
			}

			function handleImageFile(file) {
				setStatus('Đang tải ảnh…');
				uploadImageFile(file, folder, applyUrl, setStatus);
			}

			function onPaste(e) {
				var items = e.clipboardData && e.clipboardData.items;
				if (!items) return;
				for (var i = 0; i < items.length; i++) {
					if ((items[i].type || '').indexOf('image/') !== 0) continue;
					e.preventDefault();
					var file = items[i].getAsFile();
					if (file) handleImageFile(file);
					return;
				}
			}

			zone.addEventListener('paste', onPaste);
			input.addEventListener('paste', onPaste);

			zone.addEventListener('dragover', function (e) {
				e.preventDefault();
				zone.classList.add('is-drag');
			});
			zone.addEventListener('dragleave', function () {
				zone.classList.remove('is-drag');
			});
			zone.addEventListener('drop', function (e) {
				e.preventDefault();
				zone.classList.remove('is-drag');
				var files = e.dataTransfer && e.dataTransfer.files;
				if (!files || !files.length) return;
				handleImageFile(files[0]);
			});

			if (preview && input.value) {
				preview.src = input.value;
				preview.hidden = false;
			}
		});
	}

	return {
		replace: replace,
		replaceBlock: replaceBlock,
		bindForms: bindForms,
		bindMediaPickers: bindMediaPickers,
		bindMediaPasteTargets: bindMediaPasteTargets,
		bindBlockPreview: bindBlockPreview,
		refreshPreview: refreshPreview,
		writePreview: writePreview,
		buildPreviewDocument: buildPreviewDocument
	};
})();
