document.addEventListener("DOMContentLoaded", () => {
    const slider = document.querySelector(".hero-slider");
    if (!slider) return;

    const slides = [...slider.querySelectorAll(".hero-slide")];
    if (slides.length < 2) return;

    let index = 0;
    const interval = Number(slider.dataset.interval || 3000);

    setInterval(() => {
        slides[index].classList.remove("is-active");
        index = (index + 1) % slides.length;
        slides[index].classList.add("is-active");
    }, interval);
});