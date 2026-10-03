(function () {
	'use strict';

	var TITLE_IDS = ['Input_Name', 'Input_Title'];
	var DESC_IDS = ['Input_ShortDescription', 'Input_Summary', 'Input_Description', 'Input_Content'];
	var BODY_IDS = ['Input_Description', 'Input_Content'];
	var SHORT_IDS = ['Input_ShortDescription', 'Input_Summary'];
	var SLUG_IDS = ['Input_Slug'];
	var IMAGE_IDS = ['Input_ImageUrl'];
	var WATCH_IDS = TITLE_IDS.concat(DESC_IDS).concat(SLUG_IDS).concat(IMAGE_IDS);

	function q(sel, root) {
		return (root || document).querySelector(sel);
	}

	function qa(sel, root) {
		return Array.prototype.slice.call((root || document).querySelectorAll(sel));
	}

	function byId(id) {
		return document.getElementById(id);
	}

	function stripHtml(html) {
		var tmp = document.createElement('div');
		tmp.innerHTML = html || '';
		return (tmp.textContent || tmp.innerText || '').replace(/\s+/g, ' ').trim();
	}

	function fieldRaw(id) {
		var el = byId(id);
		if (!el) return '';
		if (window.CKEDITOR && CKEDITOR.instances[id]) {
			return CKEDITOR.instances[id].getData() || '';
		}
		return el.value || '';
	}

	function fieldText(id) {
		var el = byId(id);
		if (!el) return '';
		if (window.CKEDITOR && CKEDITOR.instances[id]) {
			return stripHtml(CKEDITOR.instances[id].getData() || '');
		}
		return String(el.value || '').replace(/\s+/g, ' ').trim();
	}

	function firstText(ids) {
		for (var i = 0; i < ids.length; i++) {
			var v = fieldText(ids[i]);
			if (v) return v;
		}
		return '';
	}

	function firstRaw(ids) {
		for (var i = 0; i < ids.length; i++) {
			var v = fieldRaw(ids[i]);
			if (v && String(v).trim()) return v;
		}
		return '';
	}

	function cut(text, max) {
		if (!text) return '';
		if (text.length <= max) return text;
		return text.slice(0, Math.max(0, max - 1)).replace(/\s+\S*$/, '').trimEnd() + '…';
	}

	function escapeHtml(s) {
		return String(s)
			.replace(/&/g, '&amp;')
			.replace(/</g, '&lt;')
			.replace(/>/g, '&gt;')
			.replace(/"/g, '&quot;');
	}

	function urlPrefix() {
		var path = (location.pathname || '').toLowerCase();
		if (path.indexOf('/admin/products') >= 0) return '/san-pham/';
		if (path.indexOf('/admin/posts') >= 0) return '/blog/';
		return '/';
	}

	function tone(len, idealMin, idealMax, warnMax) {
		if (len === 0) return 'empty';
		if (len < Math.max(10, Math.floor(idealMin * 0.45))) return 'bad';
		if (len < idealMin) return 'warn';
		if (len <= idealMax) return 'good';
		if (len <= warnMax) return 'warn';
		return 'bad';
	}

	function scoreTone(n) {
		if (!n || n <= 0) return 'empty';
		if (n < 50) return 'bad';
		if (n < 80) return 'warn';
		return 'good';
	}

	function updateCounter(wrap, input) {
		if (!wrap || !input) return;
		var len = (input.value || '').length;
		var idealMax = parseInt(input.getAttribute('data-seo-ideal-max'), 10) || 60;
		var idealMin = parseInt(input.getAttribute('data-seo-ideal-min'), 10) || 50;
		var warnMax = parseInt(input.getAttribute('data-seo-warn-max'), 10) || idealMax + 10;
		wrap.className = 'seo-char is-' + tone(len, idealMin, idealMax, warnMax);
		var label = q('[data-seo-count]', wrap);
		var bar = q('[data-seo-bar]', wrap);
		if (label) label.textContent = len + '/' + idealMax + ' ký tự';
		if (bar) bar.style.width = Math.min(100, Math.round((len / idealMax) * 100)) + '%';
	}

	function flash(el) {
		if (!el) return;
		el.classList.remove('is-flash');
		void el.offsetWidth;
		el.classList.add('is-flash');
	}

	function mediaThumbUrl(url) {
		if (!url) return '';
		var u = String(url).trim();
		if (!u) return '';
		if (/^https?:\/\//i.test(u) || u.indexOf('//') === 0) return u;
		if (u.indexOf('/uploads/optimized/') >= 0) {
			return u.replace(/\/uploads\/optimized\/(icon|thumb|medium|large)\//i, '/uploads/optimized/thumb/');
		}
		return u;
	}

	function hasKeyword(hay, kw) {
		if (!kw) return false;
		return String(hay || '').toLowerCase().indexOf(kw.toLowerCase()) >= 0;
	}

	function parseKeywords(raw) {
		return String(raw || '')
			.split(/[,;|]/)
			.map(function (s) { return s.replace(/\s+/g, ' ').trim(); })
			.filter(Boolean)
			.filter(function (kw, i, arr) {
				return arr.findIndex(function (x) { return x.toLowerCase() === kw.toLowerCase(); }) === i;
			})
			.slice(0, 5);
	}

	function joinKeywords(list) {
		return list.join(', ');
	}

	function highlightKeywords(text, keywords) {
		var safe = escapeHtml(text || '');
		if (!keywords || !keywords.length) return safe;
		// Longest first so multi-word phrases win over short tokens
		var ordered = keywords.slice().sort(function (a, b) { return b.length - a.length; });
		ordered.forEach(function (kw, idx) {
			var primary = keywords.indexOf(kw) === 0;
			var cls = primary ? 'seo-kw is-primary' : 'seo-kw is-secondary';
			var re;
			try {
				re = new RegExp('(' + kw.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + ')', 'ig');
			} catch (e) {
				return;
			}
			safe = safe.replace(re, function (m) {
				if (m.indexOf('<') >= 0) return m;
				return '<mark class="' + cls + '">' + m + '</mark>';
			});
		});
		// Avoid double-wrapping inside existing marks — crude cleanup
		safe = safe.replace(/<mark class="seo-kw[^"]*"><mark class="seo-kw[^"]*">/g, function () {
			return '<mark class="seo-kw is-primary">';
		}).replace(/<\/mark><\/mark>/g, '</mark>');
		return safe;
	}

	function countWords(text) {
		var t = String(text || '').trim();
		if (!t) return 0;
		return t.split(/\s+/).filter(Boolean).length;
	}

	function firstWords(text, n) {
		var parts = String(text || '').trim().split(/\s+/).filter(Boolean);
		return parts.slice(0, n).join(' ');
	}

	function keywordInHeadings(html, kw) {
		if (!html || !kw) return false;
		var re = /<h[23]\b[^>]*>([\s\S]*?)<\/h[23]>/gi;
		var m;
		while ((m = re.exec(html))) {
			if (hasKeyword(stripHtml(m[1]), kw)) return true;
		}
		return false;
	}

	function hasInternalLink(html) {
		if (!html) return false;
		var re = /<a\b[^>]*\bhref\s*=\s*["']([^"']+)["']/gi;
		var m;
		while ((m = re.exec(html))) {
			var href = m[1].trim();
			if (!href || href.charAt(0) === '#' || /^mailto:/i.test(href) || /^tel:/i.test(href)) continue;
			if (href.charAt(0) === '/' && href.indexOf('//') !== 0) return true;
			if (href.indexOf('://') >= 0 &&
				(/\/san-pham\//i.test(href) || /\/blog\//i.test(href) || /\/danh-muc\//i.test(href)))
				return true;
		}
		return false;
	}

	function collectAlbumAlts() {
		return qa('input[name="Album.AltText"], input[name="Images[].AltText"], .js-album-alt')
			.map(function (el) { return (el.value || '').trim(); })
			.filter(Boolean);
	}

	/** Mirror SeoScoreCalculator weights (primary KW + small secondary bonus). */
	function evaluateScore(ctx) {
		var keywords = ctx.keywords || parseKeywords(ctx.keyword);
		var kw = keywords[0] || '';
		var secondaries = keywords.slice(1);
		var titleShown = (ctx.metaTitle || '').trim() || ctx.title || '';
		var descShown = (ctx.metaDesc || '').trim() || ctx.shortText || cut(ctx.bodyPlain, 160);
		var slug = ((ctx.slug || '').trim()).replace(/-/g, ' ');
		var lead = firstWords(ctx.bodyPlain, 100);
		var words = countWords(ctx.bodyPlain);
		var titleLen = (ctx.metaTitle || '').trim().length || (ctx.title || '').trim().length;
		var descLen = (ctx.metaDesc || '').trim().length;
		if (!descLen) descLen = Math.min(160, (ctx.shortText || '').trim().length);
		var bodyPts = words >= 1200 ? 15 : words >= 600 ? 10 : 0;
		var altHit = !!kw && ctx.alts.some(function (a) { return hasKeyword(a, kw); });
		var secHits = secondaries.filter(function (sec) {
			return hasKeyword(titleShown, sec) || hasKeyword(descShown, sec) || hasKeyword(slug, sec);
		}).length;
		var secBonus = Math.min(6, secHits * 2);

		var items = [
			{ key: 'kw_title', label: 'Từ khóa chính trong tiêu đề', points: 15, earned: (!!kw && hasKeyword(titleShown, kw)) ? 15 : 0 },
			{ key: 'kw_desc', label: 'Từ khóa chính trong mô tả', points: 10, earned: (!!kw && hasKeyword(descShown, kw)) ? 10 : 0 },
			{ key: 'kw_slug', label: 'Từ khóa chính trong slug/URL', points: 10, earned: (!!kw && hasKeyword(slug, kw)) ? 10 : 0 },
			{ key: 'kw_lead', label: 'Từ khóa chính trong ~100 từ đầu', points: 10, earned: (!!kw && hasKeyword(lead, kw)) ? 10 : 0 },
			{ key: 'kw_heading', label: 'Từ khóa chính trong H2/H3', points: 10, earned: (!!kw && keywordInHeadings(ctx.bodyHtml, kw)) ? 10 : 0 },
			{ key: 'len_title', label: 'Meta title 50–60 ký tự', points: 10, earned: (titleLen >= 50 && titleLen <= 60) ? 10 : 0 },
			{ key: 'len_desc', label: 'Meta description 110–160 ký tự', points: 10, earned: (descLen >= 110 && descLen <= 160) ? 10 : 0 },
			{ key: 'len_body', label: 'Độ dài nội dung (≥600 / ≥1200 từ)', points: 15, earned: bodyPts },
			{ key: 'img', label: 'Có ảnh SEO hoặc ảnh đại diện', points: 5, earned: ctx.hasImage ? 5 : 0 },
			{ key: 'img_alt', label: 'Alt ảnh chứa từ khóa chính', points: 5, earned: altHit ? 5 : 0 },
			{ key: 'int_link', label: 'Có liên kết nội bộ trong nội dung', points: 10, earned: hasInternalLink(ctx.bodyHtml) ? 10 : 0 },
			{ key: 'kw_sec', label: 'Từ khóa phụ xuất hiện (tối đa +6)', points: 6, earned: secBonus }
		];

		var score = 0;
		items.forEach(function (it) { score += it.earned; });
		return { score: Math.min(100, score), items: items };
	}

	function renderBreakdown(root, result) {
		var list = q('[data-seo-score-breakdown]', root);
		var pill = q('[data-seo-score-pill]', root);
		var hint = q('[data-seo-score-hint]', root);
		var hidden = q('.js-seo-score', root);
		if (pill) {
			pill.textContent = String(result.score);
			pill.className = 'seo-score-pill is-' + scoreTone(result.score);
		}
		if (hidden) hidden.value = String(result.score);
		if (hint) {
			var label = result.score >= 80 ? 'SEO tốt' : result.score >= 50 ? 'SEO trung bình / khá' : 'SEO kém — cần tối ưu';
			hint.textContent = label + ' · điểm nhảy realtime khi sửa form';
		}
		if (!list) return;
		list.innerHTML = result.items.map(function (it) {
			var ok = it.earned > 0;
			return '<li class="' + (ok ? 'is-ok' : 'is-bad') + '">'
				+ '<i class="fa ' + (ok ? 'fa-check' : 'fa-times') + '"></i> '
				+ escapeHtml(it.label)
				+ ' <span class="seo-analyze__pts">+' + it.earned + '/' + it.points + '</span></li>';
		}).join('');
	}

	function updateImageInPreview(root, imageInput) {
		var media = q('[data-seo-preview-media]', root);
		var img = q('[data-seo-preview-img]', root);
		var badge = q('[data-seo-preview-img-badge]', root);
		if (!media || !img) return false;

		var own = imageInput && imageInput.value.trim();
		var fb = firstText(IMAGE_IDS);
		var src = mediaThumbUrl(own || fb);
		var isFallback = !own && !!fb;

		if (!src) {
			media.hidden = true;
			img.removeAttribute('src');
			if (badge) badge.hidden = true;
			return false;
		}

		if (img.getAttribute('src') !== src) img.setAttribute('src', src);
		media.hidden = false;
		if (badge) badge.hidden = !isFallback;
		img.onerror = function () {
			media.hidden = true;
			img.removeAttribute('src');
			if (badge) badge.hidden = true;
		};
		return true;
	}

	function refreshRoot(root) {
		var titleInput = q('.js-seo-meta-title', root);
		var descInput = q('.js-seo-meta-desc', root);
		var imageInput = q('.js-seo-image', root);
		var kwInput = q('.js-seo-keyword', root);
		var preview = q('[data-seo-preview]', root);
		var titleEl = q('[data-seo-preview-title]', root);
		var descEl = q('[data-seo-preview-desc]', root);
		var urlEl = q('[data-seo-preview-url]', root);
		var titleCounter = q('[data-seo-counter="title"]', root);
		var descCounter = q('[data-seo-counter="desc"]', root);

		var titleFb = firstText(TITLE_IDS);
		var shortText = firstText(SHORT_IDS);
		var bodyHtml = firstRaw(BODY_IDS);
		var bodyPlain = stripHtml(bodyHtml) || firstText(BODY_IDS);
		var slug = firstText(SLUG_IDS) || 'slug';
		var keywords = parseKeywords(kwInput ? kwInput.value : '');
		var metaTitle = titleInput ? titleInput.value.trim() : '';
		var metaDesc = descInput ? descInput.value.trim() : '';
		var titleRaw = metaTitle || titleFb || 'Tiêu đề trang';
		var descRaw = metaDesc || cut(shortText || bodyPlain, 160) || 'Mô tả trang sẽ hiện ở đây.';
		var isMobile = !!(preview && preview.classList.contains('is-mobile'));
		var nextTitle = isMobile ? cut(titleRaw, 78) : cut(titleRaw, 60);
		var nextDesc = isMobile ? cut(descRaw, 120) : cut(descRaw, 160);
		var nextUrl = (location.hostname || 'yoursite.vn') + urlPrefix() + slug;
		var titleHtml = highlightKeywords(nextTitle, keywords);
		var descHtml = highlightKeywords(nextDesc, keywords);

		if (titleEl && titleEl.innerHTML !== titleHtml) {
			titleEl.innerHTML = titleHtml;
			flash(titleEl);
		}
		if (descEl && descEl.innerHTML !== descHtml) {
			descEl.innerHTML = descHtml;
			flash(descEl);
		}
		if (urlEl) urlEl.textContent = nextUrl;

		if (titleInput) {
			titleInput.placeholder = titleFb ? ('Để trống → ' + cut(titleFb, 60)) : 'Tối ưu 50–60 ký tự';
		}
		if (descInput) {
			descInput.placeholder = shortText
				? ('Để trống → ' + cut(shortText, 90))
				: 'Tối ưu 110–160 ký tự';
		}
		if (imageInput && !imageInput.value.trim()) {
			var imgFb = firstText(IMAGE_IDS);
			imageInput.placeholder = imgFb ? ('Để trống → ' + cut(imgFb, 48)) : '/uploads/...';
		}

		var hasImage = !!updateImageInPreview(root, imageInput);
		var result = evaluateScore({
			keywords: keywords,
			title: titleFb,
			slug: slug,
			metaTitle: metaTitle,
			metaDesc: metaDesc,
			shortText: shortText,
			bodyHtml: bodyHtml,
			bodyPlain: bodyPlain,
			hasImage: hasImage || !!(imageInput && imageInput.value.trim()),
			alts: collectAlbumAlts()
		});
		renderBreakdown(root, result);
		renderKwChips(root);
		updateCounter(titleCounter, titleInput);
		updateCounter(descCounter, descInput);
	}

	function refreshAll() {
		qa('[data-seo-root]').forEach(refreshRoot);
	}

	function renderKwChips(root) {
		var hidden = q('.js-seo-keyword', root);
		var chipsEl = q('[data-seo-kw-chips]', root);
		if (!hidden || !chipsEl) return;
		var list = parseKeywords(hidden.value);
		chipsEl.innerHTML = list.map(function (kw, i) {
			var cls = i === 0 ? 'seo-kw-chip is-primary' : 'seo-kw-chip is-secondary';
			var badge = i === 0 ? '<span class="seo-kw-chip__tag">Chính</span>' : '';
			return '<span class="' + cls + '" data-kw="' + escapeHtml(kw) + '">'
				+ badge
				+ '<span class="seo-kw-chip__text">' + escapeHtml(kw) + '</span>'
				+ '<button type="button" class="seo-kw-chip__x" data-seo-kw-remove aria-label="Xóa">&times;</button>'
				+ '</span>';
		}).join('');
	}

	function syncKeywords(root, list) {
		var hidden = q('.js-seo-keyword', root);
		if (!hidden) return;
		var next = joinKeywords(list.slice(0, 5));
		if (next.length > 100) {
			// drop last until fits
			while (list.length && joinKeywords(list).length > 100) list.pop();
			next = joinKeywords(list);
		}
		hidden.value = next;
		renderKwChips(root);
		refreshRoot(root);
	}

	function bindKeywordChips(root) {
		if (root.getAttribute('data-seo-kw-bound') === '1') return;
		root.setAttribute('data-seo-kw-bound', '1');
		var box = q('[data-seo-kw-box]', root);
		var input = q('[data-seo-kw-input]', root);
		var chipsEl = q('[data-seo-kw-chips]', root);
		var hidden = q('.js-seo-keyword', root);
		if (!box || !input || !hidden) return;

		renderKwChips(root);

		function tryAdd(raw) {
			var pieces = parseKeywords(raw);
			if (!pieces.length) return;
			var list = parseKeywords(hidden.value);
			pieces.forEach(function (kw) {
				if (list.length >= 5) return;
				if (list.some(function (x) { return x.toLowerCase() === kw.toLowerCase(); })) return;
				var trial = list.concat([kw]);
				if (joinKeywords(trial).length > 100) return;
				list.push(kw);
			});
			syncKeywords(root, list);
			input.value = '';
		}

		input.addEventListener('keydown', function (e) {
			if (e.key === 'Enter' || e.key === ',') {
				e.preventDefault();
				tryAdd(input.value.replace(/,/g, ' '));
			} else if (e.key === 'Backspace' && !input.value) {
				var list = parseKeywords(hidden.value);
				if (list.length) {
					list.pop();
					syncKeywords(root, list);
				}
			}
		});

		input.addEventListener('blur', function () {
			if (input.value.trim()) tryAdd(input.value);
		});

		input.addEventListener('paste', function (e) {
			var pasted = (e.clipboardData || window.clipboardData).getData('text') || '';
			if (!/[,;|]/.test(pasted)) return;
			e.preventDefault();
			tryAdd(pasted);
		});

		if (chipsEl) {
			chipsEl.addEventListener('click', function (e) {
				var btn = e.target.closest('[data-seo-kw-remove]');
				if (!btn) return;
				var chip = btn.closest('[data-kw]');
				var remove = chip ? chip.getAttribute('data-kw') : '';
				var list = parseKeywords(hidden.value).filter(function (kw) {
					return kw.toLowerCase() !== String(remove || '').toLowerCase();
				});
				syncKeywords(root, list);
			});
		}

		box.addEventListener('click', function () { input.focus(); });
	}

	function bindTabs(root) {
		if (root.getAttribute('data-seo-tabs') === '1') return;
		root.setAttribute('data-seo-tabs', '1');
		qa('[data-seo-device]', root).forEach(function (btn) {
			btn.addEventListener('click', function () {
				var device = btn.getAttribute('data-seo-device') || 'desktop';
				qa('[data-seo-device]', root).forEach(function (b) {
					var on = b === btn;
					b.classList.toggle('is-active', on);
					b.setAttribute('aria-pressed', on ? 'true' : 'false');
				});
				var preview = q('[data-seo-preview]', root);
				if (preview) {
					preview.classList.toggle('is-mobile', device === 'mobile');
					preview.classList.toggle('is-desktop', device !== 'mobile');
				}
				refreshRoot(root);
			});
		});
	}

	function bindCkEditors() {
		if (!window.CKEDITOR) return;
		WATCH_IDS.forEach(function (id) {
			var inst = CKEDITOR.instances[id];
			if (!inst || inst.__seoBound) return;
			inst.__seoBound = true;
			inst.on('change', refreshAll);
			inst.on('key', refreshAll);
		});
	}

	function isWatched(el) {
		if (!el) return false;
		if (el.id && WATCH_IDS.indexOf(el.id) >= 0) return true;
		if (el.classList && (el.classList.contains('js-seo-meta-title')
			|| el.classList.contains('js-seo-meta-desc')
			|| el.classList.contains('js-seo-image')
			|| el.classList.contains('js-seo-keyword'))) return true;
		var name = el.getAttribute('name') || '';
		return name === 'Seo.MetaTitle' || name === 'Seo.MetaDescription' || name === 'Seo.SeoImage'
			|| name === 'Seo.SeoFocusKeyword'
			|| name === 'Input.Name' || name === 'Input.Title' || name === 'Input.Slug'
			|| name === 'Input.ShortDescription' || name === 'Input.Summary'
			|| name === 'Input.Description' || name === 'Input.Content'
			|| name === 'Input.ImageUrl'
			|| name.indexOf('AltText') >= 0;
	}

	function onFormEvent(e) {
		if (!isWatched(e.target)) return;
		refreshAll();
	}

	function bindSeoMediaPick(root) {
		qa('.js-seo-image-pick', root).forEach(function (btn) {
			if (btn.getAttribute('data-seo-pick-bound') === '1') return;
			btn.setAttribute('data-seo-pick-bound', '1');
			btn.addEventListener('click', function () {
				var inputId = btn.getAttribute('data-input') || 'Seo_SeoImage';
				var size = btn.getAttribute('data-size') || 'large';
				var folder = btn.getAttribute('data-folder') || '';
				var url = '/Admin/CkFinder/Browse?type=Images'
					+ '&size=' + encodeURIComponent(size)
					+ '&Folder=' + encodeURIComponent(folder)
					+ '&InputId=' + encodeURIComponent(inputId);
				window.open(url, 'webshop_media', 'width=1100,height=720,scrollbars=yes');
			});
		});
	}

	function init() {
		qa('[data-seo-root]').forEach(function (root) {
			bindTabs(root);
			var folder = 'khac';
			var path = (location.pathname || '').toLowerCase();
			if (path.indexOf('/admin/products') >= 0) folder = 'san-pham';
			else if (path.indexOf('/admin/posts') >= 0) folder = 'tin-tuc';
			qa('.js-seo-image-pick', root).forEach(function (btn) {
				btn.setAttribute('data-folder', folder);
			});
			bindSeoMediaPick(root);
			bindKeywordChips(root);
		});
		refreshAll();
		bindCkEditors();
		var tries = 0;
		var timer = setInterval(function () {
			bindCkEditors();
			tries += 1;
			if (tries >= 10) clearInterval(timer);
		}, 400);
	}

	document.addEventListener('input', onFormEvent, true);
	document.addEventListener('keyup', onFormEvent, true);
	document.addEventListener('change', onFormEvent, true);

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', init);
	} else {
		init();
	}

	window.WebShopSeoPreview = { refresh: refreshAll };
})();
