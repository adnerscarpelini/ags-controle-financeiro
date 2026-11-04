// Recorte do logo do banco (issue 112): a imagem aparece na área de recorte, e arrastar, a roda do mouse e o controle de zoom
// só movem os campos de zoom e posição. O recorte final continua sendo feito no servidor com esses mesmos valores.
(function () {
    "use strict";

    var LOGICAL_SIZE = 256;
    var MIN_ZOOM = 0.2;
    var MAX_ZOOM = 5;
    var objectUrl = null;
    var drag = null;
    var wheelTimer = 0;

    function parts(stage) {
        var area = stage.closest("[data-crop-area]");
        return {
            image: stage.querySelector("img"),
            zoom: area.querySelector("#logo-zoom"),
            horizontal: area.querySelector("#logo-horizontal"),
            vertical: area.querySelector("#logo-vertical"),
            percent: area.querySelector("[data-crop-percent]")
        };
    }

    function clamp(value, min, max) { return Math.min(max, Math.max(min, value)); }

    // Mesma conta de LogoProcessor, na escala da área de recorte.
    function geometry(stage, p) {
        var size = stage.clientWidth;
        var width = p.image.naturalWidth;
        var height = p.image.naturalHeight;
        var scale = Math.max(LOGICAL_SIZE / width, LOGICAL_SIZE / height) * Number(p.zoom.value) * (size / LOGICAL_SIZE);
        return { size: size, width: width * scale, height: height * scale };
    }

    function apply(stage) {
        var p = parts(stage);
        if (!p.image.naturalWidth || !stage.clientWidth) { return; }
        var g = geometry(stage, p);
        p.image.style.width = g.width + "px";
        p.image.style.height = g.height + "px";
        p.image.style.left = -(g.width - g.size) * (Number(p.horizontal.value) + 1) / 2 + "px";
        p.image.style.top = -(g.height - g.size) * (Number(p.vertical.value) + 1) / 2 + "px";
        if (p.percent && p.percent.firstChild) { p.percent.firstChild.data = Math.round(Number(p.zoom.value) * 100) + "%"; }
    }

    function commit(input) { input.dispatchEvent(new Event("change", { bubbles: true })); }

    function setFile(input) {
        var stage = input.closest("[data-crop-root]").querySelector("[data-crop-stage]");
        var file = input.files && input.files[0];
        if (!file || !stage) { return; }
        if (objectUrl) { URL.revokeObjectURL(objectUrl); }
        objectUrl = URL.createObjectURL(file);
        stage.querySelector("img").src = objectUrl;
        if (!stage.dataset.observed) {
            stage.dataset.observed = "true";
            new ResizeObserver(function () { apply(stage); }).observe(stage);
        }
    }

    document.addEventListener("change", function (e) {
        if (e.target instanceof HTMLInputElement && e.target.id === "bank-logo-file") { setFile(e.target); }
    }, true);

    document.addEventListener("load", function (e) {
        if (e.target instanceof HTMLImageElement && e.target.closest("[data-crop-stage]")) { apply(e.target.closest("[data-crop-stage]")); }
    }, true);

    document.addEventListener("input", function (e) {
        var area = e.target instanceof Element ? e.target.closest("[data-crop-area]") : null;
        if (area) { apply(area.querySelector("[data-crop-stage]")); }
    }, true);

    // O servidor pode devolver outros valores ao carregar outra imagem: a posição mostrada acompanha.
    document.addEventListener("change", function (e) {
        var area = e.target instanceof Element ? e.target.closest("[data-crop-area]") : null;
        if (area) { requestAnimationFrame(function () { apply(area.querySelector("[data-crop-stage]")); }); }
    });

    document.addEventListener("pointerdown", function (e) {
        var stage = e.target instanceof Element ? e.target.closest("[data-crop-stage]") : null;
        if (!stage || !parts(stage).image.naturalWidth) { return; }
        e.preventDefault();
        var p = parts(stage);
        drag = { stage: stage, p: p, x: e.clientX, y: e.clientY, horizontal: Number(p.horizontal.value), vertical: Number(p.vertical.value), moved: false };
        stage.setPointerCapture(e.pointerId);
        stage.classList.add("is-dragging");
    });

    document.addEventListener("pointermove", function (e) {
        if (!drag) { return; }
        var g = geometry(drag.stage, drag.p);
        var extraWidth = g.width - g.size;
        var extraHeight = g.height - g.size;
        if (Math.abs(extraWidth) > 0.5) { drag.p.horizontal.value = clamp(drag.horizontal - (e.clientX - drag.x) * 2 / extraWidth, -1, 1); }
        if (Math.abs(extraHeight) > 0.5) { drag.p.vertical.value = clamp(drag.vertical - (e.clientY - drag.y) * 2 / extraHeight, -1, 1); }
        drag.moved = true;
        apply(drag.stage);
    });

    function endDrag() {
        if (!drag) { return; }
        var finished = drag;
        drag = null;
        finished.stage.classList.remove("is-dragging");
        if (finished.moved) {
            commit(finished.p.horizontal);
            commit(finished.p.vertical);
        }
    }

    document.addEventListener("pointerup", endDrag);
    document.addEventListener("pointercancel", endDrag);

    document.addEventListener("wheel", function (e) {
        var stage = e.target instanceof Element ? e.target.closest("[data-crop-stage]") : null;
        if (!stage || !parts(stage).image.naturalWidth) { return; }
        e.preventDefault();
        var p = parts(stage);
        p.zoom.value = clamp(Number(p.zoom.value) * (e.deltaY < 0 ? 1.08 : 0.92), MIN_ZOOM, MAX_ZOOM).toFixed(2);
        apply(stage);
        clearTimeout(wheelTimer);
        wheelTimer = setTimeout(function () { commit(p.zoom); }, 250);
    }, { passive: false });

    // Preencher cobre o círculo; ajustar deixa a imagem inteira dentro dele.
    document.addEventListener("click", function (e) {
        var button = e.target instanceof Element ? e.target.closest("[data-crop-mode]") : null;
        if (!button) { return; }
        var stage = button.closest("[data-crop-area]").querySelector("[data-crop-stage]");
        var p = parts(stage);
        if (!p.image.naturalWidth) { return; }
        var width = p.image.naturalWidth;
        var height = p.image.naturalHeight;
        var fill = Math.max(LOGICAL_SIZE / width, LOGICAL_SIZE / height);
        var fit = LOGICAL_SIZE / Math.sqrt(width * width + height * height);
        var zoom = button.dataset.cropMode === "fit" ? clamp(Math.floor(fit / fill * 100) / 100, MIN_ZOOM, MAX_ZOOM) : 1;
        p.zoom.value = zoom;
        p.horizontal.value = 0;
        p.vertical.value = 0;
        apply(stage);
        commit(p.horizontal);
        commit(p.vertical);
        commit(p.zoom);
    });
})();
