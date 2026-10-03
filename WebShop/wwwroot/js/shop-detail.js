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
	const slides = [...productGallery.querySelectorAll("[data-gallery-slide]")];
	const dots = [...productGallery.querySelectorAll("[data-gallery-dot]")];
	const thumbs = [...productGallery.querySelectorAll("[data-gallery-thumb]")];
	const goTo = (index) => {
		if (!viewport || !slides[index]) return;
		const left = slides[index].offsetLeft;
		viewport.scrollTo({ left, behavior: "smooth" });
	};
	const sync = () => {
		if (!viewport || slides.length === 0) return;
		const index = Math.max(0, Math.min(slides.length - 1, Math.round(viewport.scrollLeft / Math.max(viewport.clientWidth, 1))));
		dots.forEach((dot, i) => dot.classList.toggle("is-active", i === index));
		thumbs.forEach((thumb, i) => thumb.classList.toggle("is-active", i === index));
	};
	let scrollTimer = 0;
	dots.forEach((dot) => {
		dot.addEventListener("click", () => goTo(Number(dot.getAttribute("data-gallery-dot") || "0")));
	});
	thumbs.forEach((thumb) => {
		thumb.addEventListener("click", () => goTo(Number(thumb.getAttribute("data-gallery-thumb") || "0")));
	});
	viewport?.addEventListener("scroll", () => {
		window.clearTimeout(scrollTimer);
		scrollTimer = window.setTimeout(sync, 60);
	}, { passive: true });
	sync();
})();
