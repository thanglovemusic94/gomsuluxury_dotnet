(() => {
	const loadMoreBtn = document.querySelector("[data-shop-load-more]");
	const productGrid = document.querySelector("[data-shop-product-grid]");
	if (!loadMoreBtn || !productGrid) return;

	loadMoreBtn.addEventListener("click", async (event) => {
		if (window.matchMedia("(min-width: 768px)").matches) return;
		event.preventDefault();
		if (loadMoreBtn.classList.contains("is-loading")) return;
		const cardsUrl = loadMoreBtn.getAttribute("data-cards-url");
		if (!cardsUrl) return;
		loadMoreBtn.classList.add("is-loading");
		loadMoreBtn.setAttribute("aria-busy", "true");
		try {
			const response = await fetch(cardsUrl, {
				headers: { "X-Requested-With": "XMLHttpRequest", Accept: "text/html" }
			});
			if (!response.ok) throw new Error("load-more-failed");
			const html = (await response.text()).trim();
			if (!html) {
				loadMoreBtn.closest(".shop-load-more")?.setAttribute("hidden", "");
				return;
			}
			productGrid.insertAdjacentHTML("beforeend", html);
			window.Shop?.markMediaLoaded?.(productGrid);
			window.Shop?.reveal?.(productGrid);
			const nextPage = Number(loadMoreBtn.getAttribute("data-next-page") || "1") + 1;
			const totalPages = Number(loadMoreBtn.getAttribute("data-total-pages") || "1");
			loadMoreBtn.setAttribute("data-next-page", String(nextPage));
			if (nextPage > totalPages) {
				loadMoreBtn.closest(".shop-load-more")?.setAttribute("hidden", "");
				return;
			}
			const nextNav = new URL(loadMoreBtn.href, window.location.origin);
			nextNav.searchParams.set("p", String(nextPage));
			loadMoreBtn.href = nextNav.pathname + nextNav.search;
			const nextCards = new URL(cardsUrl, window.location.origin);
			nextCards.searchParams.set("p", String(nextPage));
			loadMoreBtn.setAttribute("data-cards-url", nextCards.pathname + nextCards.search);
		} catch {
			window.location.href = loadMoreBtn.href;
		} finally {
			loadMoreBtn.classList.remove("is-loading");
			loadMoreBtn.removeAttribute("aria-busy");
		}
	});
})();
