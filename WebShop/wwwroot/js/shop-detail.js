(() => {
	const detailBack = document.querySelector("[data-detail-back]");
	if (detailBack) {
		detailBack.addEventListener("click", (event) => {
			if (window.history.length > 1) {
				event.preventDefault();
				window.history.back();
			}
		});
	}

	const toast = document.querySelector("[data-shop-toast]");
	if (toast) {
		const hideToast = () => {
			toast.classList.remove("is-visible");
			window.setTimeout(() => toast.remove(), 280);
		};
		toast.querySelector("[data-toast-close]")?.addEventListener("click", hideToast);
		window.setTimeout(hideToast, 5200);
		document.querySelectorAll(".shop-cart-count").forEach((badge) => {
			badge.classList.remove("is-pulse");
			void badge.offsetWidth;
			badge.classList.add("is-pulse");
			window.setTimeout(() => badge.classList.remove("is-pulse"), 650);
		});
		const submit = document.querySelector(".shop-buy-form__submit");
		if (submit) {
			submit.classList.add("is-pulse");
			window.setTimeout(() => submit.classList.remove("is-pulse"), 700);
		}
	}

	document.querySelectorAll("[data-readmore]").forEach((block) => {
		const clip = block.querySelector(".shop-readmore__clip");
		const btn = block.querySelector("[data-readmore-toggle]");
		if (!clip || !btn) return;
		let expanded = false;
		const measure = () => {
			if (expanded) return;
			clip.classList.add("is-clamped");
			requestAnimationFrame(() => {
				if (clip.scrollHeight > clip.clientHeight + 4) {
					btn.hidden = false;
					btn.textContent = "Xem thêm";
				} else {
					btn.hidden = true;
					clip.classList.remove("is-clamped");
				}
			});
		};
		btn.addEventListener("click", () => {
			expanded = !expanded;
			if (expanded) {
				clip.classList.remove("is-clamped");
				btn.textContent = "Thu gọn";
			} else {
				measure();
			}
		});
		measure();
		window.addEventListener("resize", measure, { passive: true });
	});

	const productGallery = document.querySelector("[data-product-gallery]");
	if (!productGallery) return;

	const viewport = productGallery.querySelector("[data-gallery-viewport]");
	const thumbsWrap = productGallery.querySelector("[data-gallery-thumbs]");
	const slides = [...productGallery.querySelectorAll("[data-gallery-slide]")];
	const dots = [...productGallery.querySelectorAll("[data-gallery-dot]")];
	const thumbs = [...productGallery.querySelectorAll("[data-gallery-thumb]")];
	const openers = [...productGallery.querySelectorAll("[data-gallery-open]")];
	const lightbox = productGallery.querySelector("[data-gallery-lightbox]");
	const lightboxViewport = lightbox?.querySelector("[data-lightbox-viewport]");
	const lightboxSlides = lightbox ? [...lightbox.querySelectorAll("[data-lightbox-slide]")] : [];
	const lightboxCount = lightbox?.querySelector("[data-lightbox-count]");
	const lightboxPrev = lightbox?.querySelector("[data-lightbox-prev]");
	const lightboxNext = lightbox?.querySelector("[data-lightbox-next]");
	const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
	const lastIndex = Math.max(0, slides.length - 1);
	let index = 0;
	let lightboxOpen = false;

	const clampIndex = (value) => Math.max(0, Math.min(lastIndex, value | 0));

	const syncFromScroller = (scroller, items) => {
		if (!scroller || items.length === 0) return 0;
		const width = Math.max(scroller.clientWidth, 1);
		return clampIndex(Math.round(scroller.scrollLeft / width));
	};

	const scrollToIndex = (scroller, items, target, smooth) => {
		if (!scroller || !items[target]) return;
		const width = Math.max(scroller.clientWidth, 1);
		const left = scroller === lightboxViewport
			? target * width
			: items[target].offsetLeft;
		scroller.scrollTo({
			left,
			behavior: smooth && !reduceMotion ? "smooth" : "auto"
		});
	};

	const syncThumbs = (active) => {
		thumbs.forEach((thumb, i) => thumb.classList.toggle("is-active", i === active));
		const activeThumb = thumbs[active];
		if (!thumbsWrap || !activeThumb || !thumbsWrap.classList.contains("shop-thumbs--strip")) return;
		const left = activeThumb.offsetLeft - (thumbsWrap.clientWidth - activeThumb.clientWidth) / 2;
		thumbsWrap.scrollTo({
			left: Math.max(0, left),
			behavior: reduceMotion ? "auto" : "smooth"
		});
	};

	const syncLightboxCount = (active) => {
		if (lightboxCount) lightboxCount.textContent = `${active + 1} / ${slides.length}`;
	};

	const updateLightboxNav = () => {
		if (lightboxPrev) {
			const atStart = index <= 0;
			lightboxPrev.disabled = atStart;
			lightboxPrev.setAttribute("aria-disabled", atStart ? "true" : "false");
		}
		if (lightboxNext) {
			const atEnd = index >= lastIndex;
			lightboxNext.disabled = atEnd;
			lightboxNext.setAttribute("aria-disabled", atEnd ? "true" : "false");
		}
	};

	const applyChrome = () => {
		dots.forEach((dot, i) => dot.classList.toggle("is-active", i === index));
		syncThumbs(index);
		syncLightboxCount(index);
		updateLightboxNav();
	};

	const goTo = (next, opts = {}) => {
		const target = clampIndex(next);
		if (target === index && (next < 0 || next > lastIndex)) {
			updateLightboxNav();
			return;
		}
		index = target;
		if (opts.main !== false) scrollToIndex(viewport, slides, index, opts.smooth !== false);
		if (lightboxOpen) scrollToIndex(lightboxViewport, lightboxSlides, index, opts.smooth !== false);
		applyChrome();
	};

	const snapLightboxToIndex = () => {
		if (!lightboxViewport) return;
		const width = Math.max(lightboxViewport.clientWidth, 1);
		lightboxSlides.forEach((slide) => {
			slide.style.flex = `0 0 ${width}px`;
			slide.style.width = `${width}px`;
			slide.style.minWidth = `${width}px`;
		});
		lightboxViewport.scrollLeft = index * width;
	};

	// Desktop: ô vuông loupe theo chuột trên ảnh chính (mousemove — tránh máy touch báo hover:none).
	const loupe = document.createElement("div");
	loupe.className = "shop-gallery__loupe";
	loupe.hidden = true;
	loupe.setAttribute("aria-hidden", "true");
	const LOUPE_SIZE = 200;
	const LOUPE_ZOOM = 1.7;
	const hideLoupe = () => { loupe.hidden = true; };
	const loupeDesktop = () => window.matchMedia("(min-width: 768px)").matches;

	const moveLoupe = (btn, event) => {
		if (!loupeDesktop() || lightboxOpen) {
			hideLoupe();
			return;
		}
		const img = btn.querySelector("img");
		if (!img) return;
		const rect = btn.getBoundingClientRect();
		if (rect.width < 80 || rect.height < 80) return;
		const half = LOUPE_SIZE / 2;
		let x = event.clientX - rect.left;
		let y = event.clientY - rect.top;
		if (x < 0 || y < 0 || x > rect.width || y > rect.height) {
			hideLoupe();
			return;
		}
		x = Math.max(half, Math.min(rect.width - half, x));
		y = Math.max(half, Math.min(rect.height - half, y));
		if (loupe.parentElement !== btn) btn.appendChild(loupe);
		loupe.hidden = false;
		loupe.style.left = `${x - half}px`;
		loupe.style.top = `${y - half}px`;
		const src = img.currentSrc || img.src;
		loupe.style.backgroundImage = `url("${src.replace(/"/g, "%22")}")`;
		loupe.style.backgroundSize = `${rect.width * LOUPE_ZOOM}px ${rect.height * LOUPE_ZOOM}px`;
		loupe.style.backgroundPosition = `${-(x * LOUPE_ZOOM - half)}px ${-(y * LOUPE_ZOOM - half)}px`;
	};

	const openLightbox = (at) => {
		if (!lightbox || !lightboxViewport) return;
		hideLoupe();
		index = clampIndex(at);
		lightboxOpen = true;
		if (lightbox.parentElement !== document.body) {
			document.body.appendChild(lightbox);
		}
		lightbox.hidden = false;
		document.documentElement.classList.add("shop-lightbox-open");
		applyChrome();
		snapLightboxToIndex();
		requestAnimationFrame(() => {
			snapLightboxToIndex();
			requestAnimationFrame(snapLightboxToIndex);
		});
		lightbox.querySelector("[data-lightbox-close]")?.focus({ preventScroll: true });
	};

	const closeLightbox = () => {
		if (!lightbox || !lightboxOpen) return;
		lightboxOpen = false;
		lightbox.hidden = true;
		document.documentElement.classList.remove("shop-lightbox-open");
		scrollToIndex(viewport, slides, index, false);
		applyChrome();
	};

	dots.forEach((dot) => {
		dot.addEventListener("click", () => goTo(Number(dot.getAttribute("data-gallery-dot") || "0")));
	});
	thumbs.forEach((thumb) => {
		thumb.addEventListener("click", () => goTo(Number(thumb.getAttribute("data-gallery-thumb") || "0")));
	});

	let pointerStartX = 0;
	let pointerStartY = 0;
	openers.forEach((btn) => {
		btn.addEventListener("pointerdown", (event) => {
			pointerStartX = event.clientX;
			pointerStartY = event.clientY;
		});
		btn.addEventListener("mousemove", (event) => moveLoupe(btn, event));
		btn.addEventListener("mouseenter", (event) => moveLoupe(btn, event));
		btn.addEventListener("mouseleave", hideLoupe);
		btn.addEventListener("click", (event) => {
			const moved = Math.hypot(event.clientX - pointerStartX, event.clientY - pointerStartY);
			if (moved > 12) return;
			hideLoupe();
			const fromBtn = Number(btn.getAttribute("data-gallery-open") || "-1");
			const visible = syncFromScroller(viewport, slides);
			openLightbox(fromBtn >= 0 ? fromBtn : visible);
		});
	});
	window.addEventListener("resize", hideLoupe, { passive: true });

	lightbox?.querySelector("[data-lightbox-close]")?.addEventListener("click", closeLightbox);
	lightboxPrev?.addEventListener("click", () => {
		if (index <= 0) return;
		goTo(index - 1);
	});
	lightboxNext?.addEventListener("click", () => {
		if (index >= lastIndex) return;
		goTo(index + 1);
	});
	lightbox?.addEventListener("click", (event) => {
		if (event.target === lightbox) closeLightbox();
	});

	document.addEventListener("keydown", (event) => {
		if (!lightboxOpen) return;
		if (event.key === "Escape") closeLightbox();
		else if (event.key === "ArrowLeft" && index > 0) goTo(index - 1);
		else if (event.key === "ArrowRight" && index < lastIndex) goTo(index + 1);
	});

	let scrollTimer = 0;
	viewport?.addEventListener("scroll", () => {
		hideLoupe();
		window.clearTimeout(scrollTimer);
		scrollTimer = window.setTimeout(() => {
			index = syncFromScroller(viewport, slides);
			applyChrome();
		}, 60);
	}, { passive: true });

	let lightboxScrollTimer = 0;
	lightboxViewport?.addEventListener("scroll", () => {
		window.clearTimeout(lightboxScrollTimer);
		lightboxScrollTimer = window.setTimeout(() => {
			index = syncFromScroller(lightboxViewport, lightboxSlides);
			applyChrome();
			scrollToIndex(viewport, slides, index, false);
		}, 60);
	}, { passive: true });

	goTo(0, { smooth: false });
})();
