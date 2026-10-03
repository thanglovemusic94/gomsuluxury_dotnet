(() => {
	const catMenu = document.querySelector("[data-shop-cat-menu]");
	if (catMenu && !catMenu.classList.contains("shop-cat-menu--home")) {
		const opener = catMenu.querySelector("[data-shop-cat-toggle]");
		const panel = catMenu.querySelector(".shop-cat-panel");
		opener?.addEventListener("click", (event) => {
			event.preventDefault();
			event.stopPropagation();
			catMenu.classList.toggle("is-open");
		});
		document.addEventListener("click", (event) => {
			if (!catMenu.contains(event.target))
				catMenu.classList.remove("is-open");
		});
		panel?.addEventListener("click", (event) => event.stopPropagation());
	}

	const search = document.querySelector("[data-shop-search]");
	if (search) {
		const input = search.querySelector("input");
		const toggle = search.querySelector("[data-search-toggle]");
		const openSearch = () => {
			search.classList.add("is-open");
			input?.focus();
		};
		const closeSearch = () => {
			if (input?.value.trim()) return;
			search.classList.remove("is-open");
			input?.blur();
		};
		toggle?.addEventListener("click", (event) => {
			event.preventDefault();
			event.stopPropagation();
			if (!search.classList.contains("is-open")) {
				openSearch();
				return;
			}
			if (input?.value.trim()) {
				search.submit();
				return;
			}
			input?.focus();
		});
		input?.addEventListener("keydown", (event) => {
			if (event.key === "Escape") {
				input.value = "";
				closeSearch();
			}
		});
		document.addEventListener("click", (event) => {
			if (!search.contains(event.target)) closeSearch();
		});
		if (input?.value.trim()) search.classList.add("is-open");
	}

	document.querySelectorAll("[data-shop-dropdown]").forEach((item) => {
		const toggle = item.querySelector("[data-shop-dropdown-toggle]");
		const panel = item.querySelector(".dropdown-menu");
		toggle?.addEventListener("click", (event) => {
			event.preventDefault();
			event.stopPropagation();
			document.querySelectorAll("[data-shop-dropdown].is-open").forEach((other) => {
				if (other !== item) {
					other.classList.remove("is-open");
					other.querySelector("[data-shop-dropdown-toggle]")?.setAttribute("aria-expanded", "false");
				}
			});
			const open = item.classList.toggle("is-open");
			toggle.setAttribute("aria-expanded", open ? "true" : "false");
		});
		panel?.addEventListener("click", (event) => event.stopPropagation());
	});
	document.addEventListener("click", () => {
		document.querySelectorAll("[data-shop-dropdown].is-open").forEach((item) => {
			item.classList.remove("is-open");
			item.querySelector("[data-shop-dropdown-toggle]")?.setAttribute("aria-expanded", "false");
		});
	});

	const backTop = document.querySelector("[data-back-top]");
	if (backTop) {
		const syncTop = () => {
			backTop.classList.toggle("is-visible", window.scrollY > 420);
		};
		backTop.addEventListener("click", () => window.scrollTo({ top: 0, behavior: "smooth" }));
		window.addEventListener("scroll", syncTop, { passive: true });
		syncTop();
	}

	const drawer = document.querySelector("[data-shop-drawer]");
	const drawerOpeners = document.querySelectorAll("[data-shop-drawer-open]");
	const setDrawer = (open) => {
		if (!drawer) return;
		drawer.classList.toggle("is-open", open);
		if (open) drawer.removeAttribute("hidden");
		else drawer.setAttribute("hidden", "");
		document.body.classList.toggle("shop-drawer-open", open);
		drawerOpeners.forEach((btn) => btn.setAttribute("aria-expanded", open ? "true" : "false"));
	};
	drawerOpeners.forEach((btn) => {
		btn.addEventListener("click", (event) => {
			event.preventDefault();
			setDrawer(true);
		});
	});
	drawer?.querySelectorAll("[data-shop-drawer-close]").forEach((el) => {
		el.addEventListener("click", () => setDrawer(false));
	});
	document.addEventListener("keydown", (event) => {
		if (event.key === "Escape") setDrawer(false);
	});

	const markMediaLoaded = (root) => {
		(root || document).querySelectorAll(".shop-media-skel > img").forEach((img) => {
			const done = () => {
				img.classList.add("is-loaded");
				img.parentElement?.classList.add("is-ready");
			};
			if (img.complete && img.naturalWidth > 0) done();
			else {
				img.addEventListener("load", done, { once: true });
				img.addEventListener("error", done, { once: true });
			}
		});
	};
	markMediaLoaded(document);

	const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
	const revealSelector = [
		".shop-card",
		".shop-post-card",
		".shop-title",
		".shop-promo",
		".shop-stage",
		".shop-cat-badges__item",
		".shop-detail-panels",
		"[data-reveal]"
	].join(",");
	const prepareReveal = (root) => {
		const scope = root || document;
		const nodes = [...scope.querySelectorAll(revealSelector)].filter((el) => {
			if (el.classList.contains("shop-reveal")) return false;
			if (el.closest(".shop-drawer, .login-dialog, .shop-tabbar")) return false;
			/* transform trên cha phá position:fixed của thanh Thêm giỏ */
			if (el.querySelector?.(".shop-buy-form") || el.closest(".shop-buy-form")) return false;
			return true;
		});
		nodes.forEach((el, index) => {
			el.classList.add("shop-reveal");
			const siblingIndex = el.parentElement
				? [...el.parentElement.children].indexOf(el)
				: index;
			const delay = Math.min(120, Math.max(0, siblingIndex) * 40);
			el.style.setProperty("--reveal-delay", `${delay}ms`);
		});
		return nodes;
	};
	const revealNow = (els) => {
		els.forEach((el) => el.classList.add("is-inview"));
	};
	const watchReveal = (els) => {
		if (!els.length) return;
		if (reduceMotion || !("IntersectionObserver" in window)) {
			revealNow(els);
			return;
		}
		const io = new IntersectionObserver((entries) => {
			entries.forEach((entry) => {
				if (!entry.isIntersecting) return;
				entry.target.classList.add("is-inview");
				io.unobserve(entry.target);
			});
		}, { rootMargin: "0px 0px -8% 0px", threshold: 0.12 });
		els.forEach((el) => {
			const rect = el.getBoundingClientRect();
			if (rect.top < window.innerHeight * 0.92 && rect.bottom > 0)
				el.classList.add("is-inview");
			else
				io.observe(el);
		});
	};
	watchReveal(prepareReveal(document));

	window.Shop = window.Shop || {};
	window.Shop.markMediaLoaded = markMediaLoaded;
	window.Shop.reveal = (root) => watchReveal(prepareReveal(root));
})();
