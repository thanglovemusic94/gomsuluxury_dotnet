(() => {
	const slider = document.querySelector("[data-shop-slider]");
	if (!slider) return;

	const track = slider.querySelector(".shop-slider__track");
	const slides = [...slider.querySelectorAll(".shop-slider__slide")];
	const dotsHost = slider.querySelector("[data-slider-dots]");
	const prev = slider.querySelector("[data-slider-prev]");
	const next = slider.querySelector("[data-slider-next]");
	if (!track || slides.length === 0 || !dotsHost) return;

	let index = 0;
	let timer = 0;
	let touchStartX = 0;
	let touchDeltaX = 0;
	let swiped = false;
	const viewport = slider.querySelector(".shop-slider__viewport") || slider;
	const go = (nextIndex, pause) => {
		index = (nextIndex + slides.length) % slides.length;
		track.style.transform = "translateX(-" + index * 100 + "%)";
		dots.forEach((dot, i) => dot.classList.toggle("is-active", i === index));
		if (pause) restart();
	};
	const restart = () => {
		clearInterval(timer);
		if (slides.length < 2) return;
		timer = window.setInterval(() => go(index + 1, false), 4500);
	};
	slides.forEach((_, i) => {
		const dot = document.createElement("button");
		dot.type = "button";
		dot.className = "shop-slider__dot";
		dot.setAttribute("aria-label", "Slide " + (i + 1));
		dot.addEventListener("click", () => go(i, true));
		dotsHost.appendChild(dot);
	});
	const dots = [...dotsHost.querySelectorAll(".shop-slider__dot")];
	prev?.addEventListener("click", () => go(index - 1, true));
	next?.addEventListener("click", () => go(index + 1, true));
	slider.addEventListener("mouseenter", () => clearInterval(timer));
	slider.addEventListener("mouseleave", restart);
	viewport.addEventListener("touchstart", (event) => {
		if (slides.length < 2) return;
		touchStartX = event.changedTouches[0].clientX;
		touchDeltaX = 0;
		swiped = false;
		clearInterval(timer);
	}, { passive: true });
	viewport.addEventListener("touchmove", (event) => {
		if (slides.length < 2) return;
		touchDeltaX = event.changedTouches[0].clientX - touchStartX;
	}, { passive: true });
	viewport.addEventListener("touchend", () => {
		if (slides.length < 2) return;
		if (Math.abs(touchDeltaX) > 40) {
			swiped = true;
			go(index + (touchDeltaX < 0 ? 1 : -1), true);
		} else {
			restart();
		}
		touchDeltaX = 0;
	}, { passive: true });
	slider.addEventListener("click", (event) => {
		if (!swiped) return;
		event.preventDefault();
		event.stopPropagation();
		swiped = false;
	}, true);
	go(0, false);
	restart();
})();
