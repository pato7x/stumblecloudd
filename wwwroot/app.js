const toast = document.querySelector("#toast");
let toastTimer;
let selectedMode = "1V1";

function showToast(message) {
  toast.textContent = message;
  toast.classList.add("visible");
  window.clearTimeout(toastTimer);
  toastTimer = window.setTimeout(() => toast.classList.remove("visible"), 2800);
}

document.querySelectorAll(".side-link").forEach((link) => {
  link.addEventListener("click", () => {
    document.querySelectorAll(".side-link").forEach((item) => item.classList.remove("selected"));
    link.classList.add("selected");
    if (link.hash !== "#inicio") showToast(`A área “${link.textContent.trim()}” ainda é uma prévia visual.`);
  });
});

document.querySelectorAll(".mode-card").forEach((card) => {
  card.addEventListener("click", () => {
    document.querySelectorAll(".mode-card").forEach((item) => item.classList.remove("mode-active"));
    card.classList.add("mode-active");
    selectedMode = card.dataset.mode;
  });
});

document.querySelectorAll(".tab").forEach((tab) => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".tab").forEach((item) => item.classList.remove("active"));
    tab.classList.add("active");
    showToast(`A classificação ${tab.textContent} será conectada aos dados do site.`);
  });
});

document.querySelector("#play-button").addEventListener("click", () => {
  showToast(`A fila ${selectedMode} será conectada ao bot na próxima etapa.`);
});

function setAvatar(elementId, avatarUrl, name) {
  const element = document.getElementById(elementId);
  if (!element) return;
  const image = document.createElement("img");
  image.src = avatarUrl;
  image.alt = `Foto de perfil de ${name}`;
  image.referrerPolicy = "no-referrer";
  image.addEventListener("error", () => {
    image.remove();
    element.textContent = name.trim().charAt(0).toUpperCase() || "D";
  }, { once: true });
  element.replaceChildren(image);
}

async function loadDiscordProfile() {
  if (window.location.protocol === "file:") return;

  try {
    const response = await fetch("/api/me", { credentials: "same-origin", cache: "no-store" });
    if (!response.ok) return;
    const profile = await response.json();
    const name = profile.displayName || profile.username || "Discord";

    document.getElementById("discord-connect").hidden = true;
    document.getElementById("discord-profile").hidden = false;
    document.getElementById("sidebar-profile").hidden = false;
    document.getElementById("top-name").textContent = name;
    document.getElementById("sidebar-name").textContent = name;
    document.getElementById("discord-greeting").textContent = name;
    document.getElementById("ranking-name").textContent = name;

    setAvatar("top-avatar", profile.avatarUrl, name);
    setAvatar("sidebar-avatar", profile.avatarUrl, name);
    setAvatar("ranking-avatar", profile.avatarUrl, name);
  } catch {
    // A página estática continua exibindo o convite de conexão.
  }
}

loadDiscordProfile();

if (window.location.protocol === "file:") {
  document.getElementById("discord-connect").addEventListener("click", (event) => {
    event.preventDefault();
    showToast("Para conectar o Discord, abra o site pelo servidor: http://localhost:5080");
  });
}

const query = new URLSearchParams(window.location.search);
if (query.get("discord") === "cancelled") {
  showToast("Conexão cancelada. Você pode tentar novamente quando quiser.");
  window.history.replaceState({}, document.title, window.location.pathname);
}
