"use strict";

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => [...document.querySelectorAll(selector)];
const icons = {
  menu: '<path d="M4 6h16M4 12h16M4 18h16"/>',
  sparkles:
    '<path d="m12 3 2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5Z"/><path d="m20 2 .5 1.5L22 4l-1.5.5L20 6l-.5-1.5L18 4l1.5-.5Z"/>',
  film: '<rect x="3" y="3" width="18" height="18" rx="3"/><path d="M7 3v18M17 3v18M3 8h4M3 16h4M17 8h4M17 16h4M7 12h10"/>',
  bookmark: '<path d="M6 4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v17l-6-4-6 4Z"/>',
  users:
    '<circle cx="9" cy="8" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3M16 5a3 3 0 0 1 0 6M18 15a5 5 0 0 1 3 4v2"/>',
  sliders:
    '<path d="M4 7h7M15 7h5M4 17h3M11 17h9"/><circle cx="13" cy="7" r="2"/><circle cx="9" cy="17" r="2"/>',
  database:
    '<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 4 16 4 16 0V5M4 12c0 4 16 4 16 0"/>',
  chevron: '<path d="m9 5 7 7-7 7"/>',
  arrow: '<path d="M4 12h16m-6-6 6 6-6 6"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  check: '<path d="m5 12 4 4L19 6"/>',
  refresh:
    '<path d="M20 7v5h-5M4 17v-5h5M5 7a8 8 0 0 1 13-2l2 3M4 16l2 3a8 8 0 0 0 13-2"/>',
  search: '<circle cx="10" cy="10" r="6"/><path d="m15 15 5 5"/>',
  download: '<path d="M12 3v12m-5-5 5 5 5-5M4 16v5h16v-5"/>',
};
function icon(name) {
  const node = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  node.setAttribute("viewBox", "0 0 24 24");
  node.setAttribute("class", "icon");
  node.setAttribute("aria-hidden", "true");
  node.innerHTML = icons[name] || icons.film; // Only local, fixed icon paths enter HTML.
  return node;
}
$$("[data-icon]").forEach((node) => node.replaceWith(icon(node.dataset.icon)));
function el(tag, text = "", className = "") {
  const node = document.createElement(tag);
  node.textContent = text;
  if (className) node.className = className;
  return node;
}
function append(parent, ...children) {
  parent.append(...children.filter(Boolean));
  return parent;
}
function button(label, run, className = "button ghost small", iconName) {
  const node = el("button", "", className);
  node.type = "button";
  if (iconName) node.append(icon(iconName));
  node.append(document.createTextNode(label));
  node.addEventListener("click", () => busy(node, run));
  return node;
}
function link(label, href, className = "button ghost small") {
  const node = el("a", label, className);
  node.href = href;
  return node;
}
let noticeTimer;
function notice(message, error = false) {
  $("#notice-text").textContent = message;
  $("#notice").classList.toggle("error", error);
  $("#notice").hidden = false;
  clearTimeout(noticeTimer);
  noticeTimer = setTimeout(
    () => {
      $("#notice").hidden = true;
    },
    error ? 12000 : 5000,
  );
}
$("#notice button").onclick = () => {
  $("#notice").hidden = true;
};
async function busy(node, operation) {
  if (node.disabled) return;
  node.disabled = true;
  node.setAttribute("aria-busy", "true");
  try {
    await operation();
  } catch (error) {
    notice(error.message, true);
  } finally {
    node.disabled = false;
    node.removeAttribute("aria-busy");
  }
}
const state = {
  environment: {},
  viewers: [],
  catalogueError: "",
  token: sessionStorage.getItem("movieWatchToken") || "",
  viewer: null,
  admin: false,
  genres: [],
  moods: [],
  moodPresets: [],
  movies: [],
  preferences: [],
  watchlist: [],
  groups: [],
  jobs: [],
  result: null,
  resultPath: "",
  watchFilter: "all",
  generation: 0,
  route: "discover",
  loading: false,
};
const routeNames = {
  discover: "Discover",
  catalogue: "Catalogue",
  watchlist: "Watchlist",
  groups: "Groups",
  preferences: "Preferences",
  admin: "Manage",
  profile: "My profile",
};

async function api(path, options = {}) {
  const token = state.token;
  const response = await fetch(path, {
    ...options,
    headers: {
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    if (response.status === 401 && token && token === state.token) {
      signOut();
      openAuth();
      throw new Error("Your session has expired. Sign in again to continue.");
    }
    throw new Error(
      problem.errors
        ? Object.values(problem.errors).flat().join(" ")
        : problem.detail ||
          problem.title ||
          `Request failed (${response.status}). Please try again.`,
    );
  }
  if (token !== state.token)
    throw new Error("The session changed. Please try again.");
  if (options.download) return response.blob();
  return response.status === 204 ? null : response.json();
}
const send = (path, method, data) =>
  api(path, {
    method,
    ...(data === undefined ? {} : { body: JSON.stringify(data) }),
  });
const personalPath = () => `/api/viewers/${state.viewer.id}`;
async function allPages(path) {
  const values = [];
  for (let skip = 0; ; skip += 100) {
    const page = await api(`${path}?skip=${skip}&take=100`);
    values.push(...page);
    if (page.length < 100) return values;
  }
}
function requireAccount() {
  if (!state.viewer) {
    openAuth();
    return false;
  }
  return true;
}
function adminFromToken(token) {
  try {
    const payload = JSON.parse(
      atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")),
    );
    const roles =
      payload.role ||
      payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];
    return [].concat(roles || []).includes("Administrator");
  } catch {
    return false;
  }
}
function sessionUI() {
  document.body.classList.toggle("has-session", !!state.viewer);
  $("#recommend-form").noValidate = !state.viewer;
  $("#preference-form").noValidate = !state.viewer;
  $("#login-button").hidden = !!state.viewer;
  $("#logout").hidden = !state.viewer;
  $("#profile-link").hidden = !state.viewer;
  syncFinder();
  $$(".admin-only").forEach((node) => {
    node.hidden = !state.admin;
  });
  if (state.viewer) {
    $("#profile-form").elements.displayName.value = state.viewer.displayName;
    $("#profile-form").elements.email.value = state.viewer.email;
    $("#viewer-id").textContent = state.viewer.id;
  }
}
function signOut() {
  cancelRecommendations();
  state.generation++;
  state.token = "";
  sessionStorage.removeItem("movieWatchToken");
  Object.assign(state, {
    viewer: null,
    admin: false,
    genres: [],
    moods: [],
    moodPresets: [],
    movies: [],
    preferences: [],
    watchlist: [],
    groups: [],
    jobs: [],
    viewers: [],
    catalogueError: "",
    result: null,
    resultPath: "",
    loading: false,
  });
  $$("dialog[open]").forEach((dialog) => dialog.close());
  $("#profile-form").reset();
  $("#viewer-id").textContent = "";
  sessionUI();
  $("#recommend-form").reset();
  $("#discovery-options").open = false;
  fillControls();
  renderAll();
  location.hash = "discover";
  navigate();
}
function navigate() {
  let route = location.hash.slice(1) || "discover";
  if (
    !routeNames[route] ||
    (route === "admin" && !state.admin) ||
    (route === "profile" && !state.viewer)
  )
    route = "discover";
  state.route = route;
  $$(".page").forEach((page) => {
    page.hidden = page.id !== `page-${route}`;
  });
  $$("[data-route]").forEach((node) => {
    if (node.dataset.route === route) node.setAttribute("aria-current", "page");
    else node.removeAttribute("aria-current");
  });
  $("#page-label").textContent = routeNames[route];
  document.title = `${routeNames[route]} · MovieWatch`;
}
function closeMenu() {
  $("#main-navigation").classList.remove("is-open");
  $("#menu-toggle").setAttribute("aria-expanded", "false");
}
$("#menu-toggle").addEventListener("click", () => {
  const open = $("#main-navigation").classList.toggle("is-open");
  $("#menu-toggle").setAttribute("aria-expanded", String(open));
});
$("#main-navigation").addEventListener("click", (event) => {
  if (event.target.closest("a[data-route]")) {
    closeMenu();
    $("#main").focus({ preventScroll: true });
  }
});
document.addEventListener("keydown", (event) => {
  if (
    event.key === "Escape" &&
    $("#menu-toggle").getAttribute("aria-expanded") === "true"
  ) {
    closeMenu();
    $("#menu-toggle").focus();
  }
});
window.addEventListener("hashchange", () => {
  closeMenu();
  navigate();
  window.scrollTo({ top: 0 });
  $("#main").focus({ preventScroll: true });
});
$("#login-button").onclick = () => openAuth();
$("#logout").onclick = $("#profile-signout").onclick = () => {
  signOut();
  notice("Signed out.");
};
function openAuth(mode = "login") {
  setAuthMode(mode);
  if (!$("#auth-dialog").open) $("#auth-dialog").showModal();
}
function setAuthMode(mode) {
  $("#login-form").hidden = mode !== "login";
  $("#register-form").hidden = mode !== "register";
  $("#auth-title").textContent =
    mode === "login" ? "Sign in" : "Create account";
  $("#auth-description").textContent =
    mode === "login"
      ? "Access your movies, watchlist, and groups."
      : "Save your preferences and find movies.";
  $$("[data-auth]").forEach((node) =>
    node.setAttribute("aria-pressed", String(node.dataset.auth === mode)),
  );
  $$("#auth-dialog .form-error").forEach((node) => {
    node.textContent = "";
  });
}
$$("[data-auth]").forEach((node) => {
  node.onclick = () => setAuthMode(node.dataset.auth);
});
$$("[data-close]").forEach((node) => {
  node.onclick = () => node.closest("dialog").close();
});
function handle(selector, operation) {
  $(selector).addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const submit = event.submitter || form.querySelector("[type=submit]");
    if (submit?.disabled) return;
    const errorTarget = form.querySelector(".form-error");
    if (errorTarget) errorTarget.textContent = "";
    if (submit) {
      submit.disabled = true;
      submit.setAttribute("aria-busy", "true");
    }
    try {
      await operation(form, Object.fromEntries(new FormData(form)));
    } catch (error) {
      if (errorTarget) errorTarget.textContent = error.message;
      else notice(error.message, true);
    } finally {
      if (submit) {
        submit.disabled = false;
        submit.removeAttribute("aria-busy");
      }
    }
  });
}
async function login(credentials) {
  const result = await send("/api/auth/login", "POST", credentials);
  state.token = result.accessToken;
  sessionStorage.setItem("movieWatchToken", state.token);
  await bootstrap();
  if (!state.viewer)
    throw new Error(
      "Unable to load your profile. Please try signing in again.",
    );
  $("#auth-dialog").close();
  $("#login-form").reset();
  $("#register-form").reset();
  notice(`Welcome, ${state.viewer.displayName}.`);
}
handle("#login-form", async (_, data) => login(data));
handle("#register-form", async (_, data) => {
  await send("/api/auth/register", "POST", data);
  await login({ email: data.email, password: data.password });
});

function fillSelect(select, values, placeholder, label = (item) => item.name) {
  const selected = select.value;
  select.disabled = values.length === 0;
  select.replaceChildren(new Option(placeholder, ""));
  values.forEach((value) => select.add(new Option(label(value), value.id)));
  if (values.some((value) => value.id === selected)) select.value = selected;
}
function fillControls() {
  $$(".mood-select").forEach((select) =>
    fillSelect(
      select,
      state.moods,
      state.viewer
        ? state.moods.length
          ? "Choose a mood"
          : "No moods yet — ask an administrator"
        : "Sign in to choose a mood",
    ),
  );
  $$(".genre-select").forEach((select) =>
    fillSelect(
      select,
      state.genres,
      state.genres.length ? "Choose a genre" : "No genres yet — sync in Manage",
    ),
  );
  $$(".group-select").forEach((select) =>
    fillSelect(select, state.groups, "Choose a group"),
  );
  fillSelect($("#catalogue-genre"), state.genres, "All genres");
  $("#catalogue-genre").disabled = false;
  const checked = $$("input[name=genreIds]:checked").map(
    (input) => input.value,
  );
  $("#recommend-genres").replaceChildren();
  for (const genre of state.genres) {
    const label = el("label");
    const input = el("input");
    input.type = "checkbox";
    input.name = "genreIds";
    input.value = genre.id;
    input.checked = checked.includes(genre.id);
    append(label, input, el("span", genre.name));
    $("#recommend-genres").append(label);
  }
  if (!state.genres.length)
    $("#recommend-genres").append(
      el("span", "No genres available yet.", "muted"),
    );
  syncFinder();
  renderMoodAdjustments();
}
async function refreshCatalogue() {
  const generation = state.generation;
  const [genres, moods, movies, moodPresets] = await Promise.all([
    allPages("/api/genres"),
    allPages("/api/moods"),
    allPages("/api/movies"),
    api("/api/moods/presets"),
  ]);
  if (generation !== state.generation) return;
  genres.sort((a, b) => a.name.localeCompare(b.name));
  moods.sort((a, b) => a.name.localeCompare(b.name));
  Object.assign(state, { genres, moods, movies, moodPresets, catalogueError: "" });
  fillControls();
  renderCatalogue();
  renderPreferences();
  renderWatchlist();
  renderAdmin();
  renderSetup();
  renderMoodAdjustments();
  scheduleRecommendations();
}
async function refreshPersonal() {
  const generation = state.generation;
  const [preferences, watchlist] = await Promise.all([
    allPages(`${personalPath()}/preferences`),
    allPages(`${personalPath()}/watchlist`),
  ]);
  if (generation !== state.generation) return;
  Object.assign(state, { preferences, watchlist });
  renderMoodAdjustments();
  scheduleRecommendations();
  renderPreferences();
  renderWatchlist();
  renderCatalogue();
  renderRecommendations();
}
async function refreshGroups() {
  const generation = state.generation;
  const groups = await allPages("/api/groups");
  if (generation !== state.generation) return;
  state.groups = groups;
  $$(".group-select").forEach((select) =>
    fillSelect(select, state.groups, "Choose a group"),
  );
  renderGroups();
  syncFinder();
}
async function refreshViewers() {
  const generation = state.generation;
  const viewers = await allPages("/api/viewers");
  if (generation !== state.generation) return;
  state.viewers = viewers;
  renderViewers();
}
async function refreshImports() {
  const generation = state.generation;
  const jobs = await allPages("/api/import-jobs");
  if (generation !== state.generation) return;
  state.jobs = jobs;
  renderImports();
}
async function bootstrap() {
  if (!state.token) return;
  const generation = ++state.generation;
  state.loading = true;
  renderAll();
  try {
    const viewer = await api("/api/viewers/me");
    if (generation !== state.generation) return;
    state.viewer = viewer;
    state.admin = adminFromToken(state.token);
    sessionUI();
    try {
      await refreshCatalogue();
    } catch (error) {
      state.catalogueError = error.message;
      notice(error.message, true);
    }
    const results = await Promise.allSettled([
      refreshPersonal(),
      refreshGroups(),
      ...(state.admin ? [refreshImports(), refreshViewers()] : []),
    ]);
    for (const result of results)
      if (result.status === "rejected") notice(result.reason.message, true);
  } catch (error) {
    notice(error.message, true);
  } finally {
    if (generation === state.generation) {
      state.loading = false;
      renderAll();
      navigate();
    }
  }
}
function empty(
  target,
  title,
  description,
  actionLabel,
  run,
  iconName = "film",
) {
  const box = el("div", "", "empty-state");
  append(box, icon(iconName), el("h3", title), el("p", description));
  if (actionLabel) box.append(button(actionLabel, run, "button ghost small"));
  $(target).replaceChildren(box);
}
function signedOutState(target, description) {
  if (state.loading) {
    $(target).replaceChildren(el("div", "Loading…", "loading-state loading"));
    return true;
  }
  if (state.viewer) return false;
  empty(
    target,
    "Sign in to continue",
    description,
    "Sign in to get started",
    () => openAuth(),
    "sparkles",
  );
  return true;
}
function renderAll() {
  renderSetup();
  renderViewers();
  renderCatalogue();
  renderWatchlist();
  renderPreferences();
  renderGroups();
  renderRecommendations();
  renderAdmin();
}
const movieFor = (id) => state.movies.find((movie) => movie.id === id);
const nameFor = (values, id) =>
  values.find((value) => value.id === id)?.name || "Unavailable";
function movieMeta(movie) {
  return `${movie.releaseDate?.slice(0, 4) || "Release date unknown"} · ${movie.runtimeMinutes ? `${movie.runtimeMinutes} min` : "Runtime unknown"}`;
}
function genreTags(movie) {
  return append(
    el("div", "", "movie-genres"),
    ...movie.genres.map((genre) => el("span", genre.name)),
  );
}
function saveButton(movie) {
  const saved = state.watchlist.find((entry) => entry.movieId === movie.id);
  const control = saved
    ? link(saved.status === "Watched" ? "Watched" : "In watchlist", "#watchlist", "button saved small save-button")
    : button("Watchlist", () => saveMovie(movie), "button ghost small save-button", "plus");
  if (saved) {
    control.prepend(icon("check"));
    // A link inside a modal must close it before navigating behind it.
    control.addEventListener("click", () => $("#detail-dialog").close());
  }
  control.setAttribute("aria-label", saved
    ? `Open watchlist: ${movie.title} is ${saved.status === "Watched" ? "watched" : "saved"}`
    : `Add ${movie.title} to watchlist`);
  return control;
}
async function saveMovie(movie) {
  if (!requireAccount()) return;
  await send(`${personalPath()}/watchlist`, "POST", {
    movieId: movie.id,
    status: "Planned",
    note: "",
  });
  await refreshPersonal();
  notice(`“${movie.title}” is on your watchlist.`);
  if ($("#detail-dialog").open) showMovie(movie);
}
function movieThumbnail(movie) {
  const thumbnail = el("div", "", "movie-thumbnail");
  thumbnail.setAttribute("aria-hidden", "true");
  const fallback = () => {
    thumbnail.classList.add("poster-fallback");
    thumbnail.replaceChildren(icon("film"), el("span", movie.title, "poster-title"), el("span", "Poster unavailable", "poster-caption"));
  };
  if (
    !/^\/[A-Za-z0-9_-]+\.(jpg|jpeg|png|webp)$/i.test(movie.posterPath || "")
  ) {
    fallback();
    return thumbnail;
  }
  const image = el("img");
  image.alt = "";
  image.width = 342;
  image.height = 513;
  image.loading = "lazy";
  image.decoding = "async";
  image.src = `https://image.tmdb.org/t/p/w342${movie.posterPath}`;
  image.addEventListener("error", fallback, { once: true });
  thumbnail.append(image);
  return thumbnail;
}
function shortMatchReason(recommendation) {
  if (recommendation.score <= 0) return "An alternative beyond your positive mood matches.";
  if (recommendation.memberScores.length)
    return `Based on ${recommendation.memberScores.length} participating viewers’ combined preferences.`;
  const favored = recommendation.genreContributions.filter((genre) => genre.weight > 0).map((genre) => genre.genreName);
  return favored.length ? `${favored.slice(0, 2).join(" & ")}${favored.length > 2 ? " and more" : ""}: a fit for this mood.` : "A positive fit for your mood preferences.";
}
function movieCard(movie, recommendation, entry) {
  const card = el("article", "", "movie-card");
  card.dataset.movieId = movie.id;
  const poster = button("", () => showMovie(movie, recommendation), "movie-poster-button");
  poster.setAttribute("aria-label", `View ${movie.title}`);
  poster.append(movieThumbnail(movie));
  if (recommendation) {
    const positive = recommendation.score > 0;
    poster.append(append(el("span", "", `movie-badge${positive ? " positive" : ""}`),
      icon(positive ? "sparkles" : "film"), document.createTextNode(positive ? "Mood match" : "Other option")));
  }
  card.append(poster);
  const body = el("div", "", "movie-body");
  append(body,
    append(el("h3"), button(movie.title, () => showMovie(movie, recommendation), "movie-title-button")),
    el("p", movieMeta(movie), "movie-meta"),
    genreTags(movie),
  );
  if (recommendation)
    body.append(append(el("p", "", "match-reason"), icon("sparkles"), el("span", shortMatchReason(recommendation))));
  if (entry)
    body.append(
      el(
        "p",
        entry.status === "Watched" ? "✓ Watched" : "◷ Want to watch",
        "score",
      ),
    );
  if (entry?.note) body.append(el("p", entry.note, "movie-note"));
  else
    body.append(
      el(
        "p",
        movie.overview || "Open the details to explore this movie.",
        "movie-overview",
      ),
    );
  const actions = el("div", "", "actions");
  if (entry) {
    append(
      actions,
      button(
        entry.status === "Watched" ? "Mark planned" : "Mark watched",
        async () => {
          await send(`${personalPath()}/watchlist/${entry.id}`, "PUT", {
            status: entry.status === "Watched" ? "Planned" : "Watched",
            note: entry.note,
          });
          await refreshPersonal();
          notice("Watchlist updated.");
        },
        "button ghost small",
        "check",
      ),
      button("Edit note", () => editWatchlist(entry, movie)),
    );
  } else
    append(
      actions,
      button(recommendation ? "Why it fits" : "Details", () =>
        showMovie(movie, recommendation),
      ),
      saveButton(movie),
    );
  body.append(actions);
  card.append(body);
  return card;
}
function signedScore(value) {
  return `${value > 0 ? "+" : ""}${Number(value.toFixed(2))}`;
}
function renderCatalogue() {
  if (
    signedOutState(
      "#movies",
      "Sign in to explore the catalogue and save something for later.",
    )
  ) {
    $("#catalogue-count").textContent = "";
    return;
  }
  const query = $("#movie-search").value.trim().toLowerCase();
  const genre = $("#catalogue-genre").value;
  const movies = state.movies.filter(
    (movie) =>
      (!query ||
        `${movie.title} ${movie.overview}`.toLowerCase().includes(query)) &&
      (!genre || movie.genres.some((value) => value.id === genre)),
  );
  const sort = $("#catalogue-sort").value;
  movies.sort((a, b) =>
    sort === "title"
      ? a.title.localeCompare(b.title)
      : sort === "newest"
        ? (b.releaseDate || "").localeCompare(a.releaseDate || "")
        : sort === "shortest"
          ? (a.runtimeMinutes || Infinity) - (b.runtimeMinutes || Infinity)
          : b.voteCount - a.voteCount || a.title.localeCompare(b.title),
  );
  $("#catalogue-count").textContent =
    `${movies.length} ${movies.length === 1 ? "movie" : "movies"}${query || genre ? ` matching your filters · ${state.movies.length} in the catalogue` : " to discover"}`;
  if (!movies.length) {
    if (state.movies.length)
      empty(
        "#movies",
        "No movies match just yet",
        "Try a different title or clear your filters.",
        "Clear filters",
        () => {
          $("#movie-search").value = "";
          $("#catalogue-genre").value = "";
          renderCatalogue();
        },
        "search",
      );
    else
      empty(
        "#movies",
        "No movies yet",
        state.admin
          ? "Add a movie or import popular movies from TMDB to get your catalogue started."
          : "Your catalogue is empty. An administrator can add movies or import them from TMDB.",
        state.admin ? "Manage" : "",
        () => {
          location.hash = "admin";
        },
      );
    return;
  }
  $("#movies").replaceChildren(...movies.map((movie) => movieCard(movie)));
}
$("#movie-search").oninput = renderCatalogue;
$("#catalogue-genre").onchange = renderCatalogue;
$("#catalogue-sort").onchange = renderCatalogue;
$("#refresh-catalogue").onclick = () => {
  if (requireAccount())
    busy($("#refresh-catalogue"), async () => {
      await refreshCatalogue();
      notice("Catalogue refreshed.");
    });
};
function showMovie(movie, recommendation) {
  const content = $("#detail-content");
  content.replaceChildren();
  const title = el("h2", movie.title);
  title.id = "detail-title";
  append(
    content,
    movieThumbnail(movie),
    title,
    el("p", movieMeta(movie), "movie-meta"),
    genreTags(movie),
    el("p", movie.overview || "No overview is available for this movie yet."),
  );
  if (recommendation) {
    const box = el("div", "", "explanation");
    append(
      box,
      el("h3", recommendation.score > 0 ? "Why it fits" : "About this alternative"),
      el("p", recommendation.explanation),
      el("p", `Mood score: ${signedScore(recommendation.score)}. Above zero is a positive match; this is not a movie rating.`, "form-hint"),
    );
    for (const contribution of recommendation.genreContributions)
      box.append(
        append(
          el("div", "", "contribution"),
          el("span", contribution.genreName),
          el("strong", signedScore(contribution.weight)),
        ),
      );
    recommendation.memberScores.forEach((member, index) =>
      box.append(
        append(
          el("div", "", "contribution"),
          el(
            "span",
            member.viewerId === state.viewer.id
              ? "You"
              : viewerName(member.viewerId, `Viewer ${index + 1}`),
          ),
          el("strong", signedScore(member.score)),
        ),
      ),
    );
    content.append(box);
  }
  const actions = append(el("div", "", "actions"), saveButton(movie));
  if (state.admin)
    append(
      actions,
      button("Edit movie", () => editMovie(movie)),
      button(
        "Delete",
        () =>
          confirmDelete(
            `Delete “${movie.title}”?`,
            "Movies saved in a watchlist cannot be deleted until those entries are removed.",
            async () => {
              await send(`/api/movies/${movie.id}`, "DELETE");
              $("#detail-dialog").close();
              state.result = null;
              await refreshCatalogue();
              renderRecommendations();
              notice("Movie deleted.");
            },
          ),
        "button danger small",
      ),
    );
  content.append(actions);
  append(content,
    el(
      "p",
      movie.tmdbId
        ? `Source: TMDB · ID ${movie.tmdbId}`
        : "Source: manually added",
      "movie-source",
    ),
  );
  if (!$("#detail-dialog").open) $("#detail-dialog").showModal();
}
function renderWatchlist() {
  $("#watchlist-count").textContent = state.watchlist.length;
  if (
    signedOutState(
      "#watchlist",
      "Keep all your want-to-watch and already-watched movies in one place.",
    )
  )
    return;
  const entries = state.watchlist.filter(
    (entry) =>
      state.watchFilter === "all" || entry.status === state.watchFilter,
  );
  if (!entries.length) {
    empty(
      "#watchlist",
      state.watchFilter === "Watched"
        ? "Your credits haven’t rolled yet"
        : "Make room for your next favorite",
      "Save movies from the catalogue or your recommendations. Mark them watched after movie night.",
      "Explore the catalogue",
      () => {
        location.hash = "catalogue";
      },
      "bookmark",
    );
    return;
  }
  $("#watchlist").replaceChildren(
    ...entries.map((entry) =>
      movieCard(
        movieFor(entry.movieId) || {
          id: entry.movieId,
          title: "Unavailable movie",
          genres: [],
          overview: "",
        },
        null,
        entry,
      ),
    ),
  );
}
$$("[data-filter]").forEach((node) => {
  node.onclick = () => {
    state.watchFilter = node.dataset.filter;
    $$("[data-filter]").forEach((tab) =>
      tab.setAttribute("aria-pressed", String(tab === node)),
    );
    renderWatchlist();
  };
});
function editWatchlist(entry, movie) {
  openEdit(
    `Your notes on ${movie.title}`,
    [
      field("status", "Watch status", "select", entry.status, [
        ["Planned", "Want to watch"],
        ["Watched", "Watched"],
      ]),
      field("note", "Personal note", "textarea", entry.note, null, {
        maxlength: 1000,
      }),
    ],
    async (data) => {
      await send(`${personalPath()}/watchlist/${entry.id}`, "PUT", data);
      await refreshPersonal();
      notice("Watchlist updated.");
    },
    "Keep a thought, a recommendation, or a reason to watch.",
  );
  $("#edit-fields").append(
    button(
      "Remove from watchlist",
      () => {
        $("#edit-dialog").close();
        confirmDelete(
          "Remove this movie?",
          "This removes your saved status and note.",
          async () => {
            await send(`${personalPath()}/watchlist/${entry.id}`, "DELETE");
            await refreshPersonal();
            notice("Movie removed from watchlist.");
          },
        );
      },
      "button danger small",
    ),
  );
}
function renderPreferences() {
  $("#preference-count").textContent =
    `${state.preferences.length} preferences`;
  if (
    signedOutState(
      "#preferences",
      "Tell us what you love for each mood, and make your recommendations more personal.",
    )
  )
    return;
  if (!state.preferences.length) {
    empty(
      "#preferences",
      "What makes a movie your kind of movie?",
      "Mood presets work immediately. Add preferences to adjust them to your taste.",
      "",
      null,
      "sliders",
    );
    return;
  }
  $("#preferences").replaceChildren(
    ...state.preferences.map((preference) => {
      const text = append(
        el("div"),
        el("strong", nameFor(state.genres, preference.genreId)),
        el(
          "p",
          `When you’re feeling ${nameFor(state.moods, preference.moodId).toLowerCase()}`,
        ),
      );
      return append(
        el("div", "", "data-row"),
        text,
        append(
          el("div", "", "actions"),
          el("span", signedScore(preference.weight), "weight"),
          button("Edit", () =>
            openEdit(
              "Change your preference",
              [weightField(preference.weight)],
              async (data) => {
                await send(
                  `${personalPath()}/preferences/${preference.id}`,
                  "PUT",
                  { weight: Number(data.weight) },
                );
                await refreshPersonal();
                notice("Preference updated.");
              },
            ),
          ),
          button("Remove", () =>
            confirmDelete(
              "Remove this preference?",
              "This genre will return to the preset default for this mood.",
              async () => {
                await send(
                  `${personalPath()}/preferences/${preference.id}`,
                  "DELETE",
                );
                await refreshPersonal();
                notice("Preference removed.");
              },
            ),
          ),
        ),
      );
    }),
  );
}
function weightField(value) {
  return field("weight", "How does this genre fit?", "select", String(value), [
    ["2", "Love it (+2)"],
    ["1", "Like it (+1)"],
    ["0", "Neutral (0)"],
    ["-1", "Not my first choice (−1)"],
    ["-2", "Not for this mood (−2)"],
  ]);
}
handle("#preference-form", async (_, data) => {
  if (!requireAccount()) return;
  const existing = state.preferences.find(
    (value) => value.genreId === data.genreId && value.moodId === data.moodId,
  );
  await send(
    `${personalPath()}/preferences${existing ? `/${existing.id}` : ""}`,
    existing ? "PUT" : "POST",
    existing
      ? { weight: Number(data.weight) }
      : { ...data, weight: Number(data.weight) },
  );
  await refreshPersonal();
  notice(
    "Your preference is saved. Your recommendations are updating.",
  );
});

function recommendationPath(form) {
  const data = Object.fromEntries(new FormData(form));
  if (!data.moodId)
    throw new Error(
      "Choose a mood first. If no moods are available, an administrator can add one.",
    );
  if (data.scope === "group" && !data.groupId)
    throw new Error("Choose a group to find movies together.");
  const path =
    data.scope === "group" ? `/api/groups/${data.groupId}` : personalPath();
  const query = new URLSearchParams({
    moodId: data.moodId,
    maximumRuntimeMinutes: data.maximumRuntimeMinutes,
    limit: data.limit,
    includeWatched: String(form.elements.includeWatched.checked),
    includeOtherOptions: String(showOtherOptions),
    skip: String(recommendationSkip),
  });
  new FormData(form)
    .getAll("genreIds")
    .forEach((id) => query.append("genreIds", id));
  return `${path}/recommendations?${query}`;
}
function syncFinder() {
  const form = $("#recommend-form");
  const group = form.elements.scope.value === "group";
  $("#finder-hint").lastChild.textContent = !state.viewer
    ? "Sign in to get personal picks, save movies, and watch together."
    : form.elements.moodId.value ? "Matches update as you change your choices." : "Pick a mood to begin. Matches update automatically.";
  $("#recommend-group-field").hidden = !group;
  form.elements.groupId.required = group;
  $("#find-label").textContent = !state.viewer ? "Sign in to find movies" : group ? "Find our movies" : "Find my movies";
  $$("[data-runtime]").forEach((node) => node.setAttribute("aria-pressed", String(node.dataset.runtime === form.elements.maximumRuntimeMinutes.value)));
  const filters = [];
  const genreCount = form.querySelectorAll("[name=genreIds]:checked").length;
  if (genreCount) filters.push(`${genreCount} ${genreCount === 1 ? "genre" : "genres"}`);
  if (form.elements.includeWatched.checked) filters.push("including watched");
  $("#filter-summary").textContent = filters.length ? filters.join(" · ") : "Optional filters & mood preferences";
  const guidance = $("#group-guidance");
  guidance.hidden = !group || !state.viewer;
  guidance.replaceChildren();
  if (guidance.hidden) return;
  if (!state.groups.length) {
    append(guidance, document.createTextNode("Bring your people together. "), link("Create a group", "#groups", "inline-link"));
    return;
  }
  const selected = state.groups.find((value) => value.id === form.elements.groupId.value);
  guidance.textContent = selected
    ? `${selected.memberships.filter((member) => member.includedInRecommendations).length} participating viewers · Everyone’s preferences count equally.`
    : "Choose a group. Everyone participating has an equal say.";
}
$("#recommend-form").elements.scope.onchange = syncFinder;
$$("[data-runtime]").forEach((node) => {
  node.onclick = () => {
    $("#recommend-form").elements.maximumRuntimeMinutes.value = node.dataset.runtime;
    syncFinder();
    scheduleRecommendations();
  };
});
$("#reset-filters").onclick = () => {
  const form = $("#recommend-form");
  form.querySelectorAll("[name=genreIds]").forEach((input) => { input.checked = false; });
  form.elements.includeWatched.checked = false;
  form.elements.limit.value = "6";
  syncFinder();
  scheduleRecommendations();
};
let recommendationVersion = 0;
let recommendationTimer;
let recommendationAbort;
let showOtherOptions = false;
let recommendationSkip = 0;
function cancelRecommendations() {
  recommendationVersion++;
  clearTimeout(recommendationTimer);
  recommendationAbort?.abort();
}
function scheduleRecommendations(resetOptions = true) {
  cancelRecommendations();
  recommendationSkip = 0;
  if (resetOptions) showOtherOptions = false;
  state.result = null;
  state.resultPath = "";
  renderRecommendations();
  const form = $("#recommend-form");
  if (!state.viewer || !form.elements.moodId.value ||
      (form.elements.scope.value === "group" && !form.elements.groupId.value) ||
      !form.checkValidity()) return;
  $("#recommendations").replaceChildren(el("div", "Finding movies…", "loading-state loading"));
  recommendationTimer = setTimeout(() => loadRecommendations(form), 180);
}
async function loadRecommendations(form) {
  cancelRecommendations();
  if (!state.viewer || !form.elements.moodId.value ||
      (form.elements.scope.value === "group" && !form.elements.groupId.value) ||
      !form.checkValidity()) return;
  const version = recommendationVersion;
  const generation = state.generation;
  const path = recommendationPath(form);
  const controller = new AbortController();
  recommendationAbort = controller;
  state.result = null;
  state.resultPath = "";
  renderRecommendations();
  $("#recommendations").replaceChildren(el("div", "Finding movies…", "loading-state loading"));
  try {
    const result = await api(path, { signal: controller.signal });
    if (version !== recommendationVersion || generation !== state.generation) return;
    if (result.skip > 0 && result.skip >= result.totalCount) {
      recommendationSkip = Math.max(0, Math.ceil(result.totalCount / result.limit) - 1) * result.limit;
      await loadRecommendations(form);
      return;
    }
    state.result = result;
    state.resultPath = path;
    renderRecommendations();
  } catch (error) {
    if (version !== recommendationVersion || generation !== state.generation || error.name === "AbortError") return;
    renderRecommendations();
    empty("#recommendations", "Could not load matches", error.message, "Try again", () => loadRecommendations(form));
  }
}
handle("#recommend-form", async (form) => {
  recommendationSkip = 0;
  if (requireAccount()) await loadRecommendations(form);
});
$("#recommend-form").addEventListener("input", (event) => {
  if (event.target.type === "number") {
    syncFinder();
    scheduleRecommendations();
  }
});
$("#recommend-form").addEventListener("change", (event) => {
  if (event.target.closest("#mood-adjustments") || event.target.type === "number") return;
  syncFinder();
  renderMoodAdjustments();
  scheduleRecommendations();
});
$("#other-options").onclick = () => {
  showOtherOptions = !showOtherOptions;
  scheduleRecommendations(false);
};
async function changeRecommendationPage(direction) {
  if (!state.result) return;
  const { skip, limit, totalCount } = state.result;
  const next = skip + direction * limit;
  if (next < 0 || next >= totalCount) return;
  recommendationSkip = next;
  await loadRecommendations($("#recommend-form"));
  $("#results-title").scrollIntoView({ block: "start" });
}
$("#recommendation-previous").onclick = () => changeRecommendationPage(-1);
$("#recommendation-next").onclick = () => changeRecommendationPage(1);
function presetWeight(mood, genre) {
  return mood.defaultWeights?.find((weight) => genre.tmdbId != null
    ? weight.tmdbId === genre.tmdbId
    : weight.genreName.toLowerCase() === genre.name.toLowerCase())?.weight || 0;
}
function renderMoodAdjustments() {
  const mood = state.moods.find((m) => m.id === $("#recommend-form").elements.moodId.value);
  $("#mood-adjustments").hidden = !mood;
  $("#mood-description").textContent = mood
    ? (mood.presetKey ? mood.description : "Custom mood: open Fine-tune your picks to set your genre preferences.")
    : "";
  $("#mood-weights").replaceChildren();
  if (!mood) return;
  if (!state.genres.length) {
    $("#mood-weights").append(el("p", "No genres available yet. An administrator can sync genres in Manage.", "muted"));
    return;
  }
  for (const genre of state.genres) {
    const preference = state.preferences.find((p) => p.moodId === mood.id && p.genreId === genre.id);
    const weight = presetWeight(mood, genre);
    const select = el("select");
    select.add(new Option(`Preset: ${weight > 0 ? "Favor" : weight < 0 ? "Avoid" : "Neutral"}`, "default"));
    [["2", "Favor"], ["0", "Neutral"], ["-2", "Avoid (lower rank)"]].forEach(([value, label]) => select.add(new Option(label, value)));
    if (preference && Math.abs(preference.weight) === 1)
      select.add(new Option(preference.weight > 0 ? "Slightly favor (saved)" : "Slightly avoid (saved)", String(preference.weight)));
    select.value = preference ? String(preference.weight) : "default";
    select.onchange = async () => {
      select.disabled = true;
      try {
        const path = `${personalPath()}/preferences`;
        if (select.value === "default") {
          if (preference) await send(`${path}/${preference.id}`, "DELETE");
        } else {
          await send(preference ? `${path}/${preference.id}` : path, preference ? "PUT" : "POST",
            preference ? { weight: Number(select.value) } : { moodId: mood.id, genreId: genre.id, weight: Number(select.value) });
        }
        await refreshPersonal();
        notice("Mood preference saved.");
      } catch (error) {
        select.value = preference ? String(preference.weight) : "default";
        notice(error.message, true);
      } finally { select.disabled = false; }
    };
    $("#mood-weights").append(append(el("label"), el("span", genre.name), select));
  }
}
function renderRecommendations() {
  $("#match-summary").textContent = "";
  $("#other-options").hidden = true;
  $("#export").hidden = !state.result;
  $("#recommendation-pagination").hidden = true;
  if (
    signedOutState(
      "#recommendations",
      "Sign in, choose your mood, and we’ll find movies that fit your time and taste.",
    )
  ) {
    $("#results-title").textContent = "Picked for your evening";
    return;
  }
  if (!state.result) {
    $("#results-title").textContent = "Picked for your evening";
    empty(
      "#recommendations",
      $("#recommend-form").elements.scope.value === "group" && !$("#recommend-form").elements.groupId.value ? "Who’s watching tonight?" : "A mood is all it takes",
      $("#recommend-form").elements.scope.value === "group" && !$("#recommend-form").elements.groupId.value
        ? "Choose a group above, or create one in Groups to find a movie together."
        : state.moods.length
        ? "Choose your mood, set your time, and your movie picks will appear here."
        : "Your catalogue needs a mood to get started. An administrator can add moods in Manage.",
      "",
      null,
      "sparkles",
    );
    return;
  }
  const results = state.result.recommendations;
  $("#results-title").textContent =
    $("#recommend-form").elements.scope.value === "group" ? "Picks for your group" : "Your picks for tonight";
  const positive = state.result.positiveMatchCount;
  const other = state.result.otherOptionCount;
  const { skip, limit, totalCount } = state.result;
  if (totalCount > 0) {
    $("#recommendation-pagination").hidden = false;
    $("#recommendation-previous").hidden = totalCount <= limit;
    $("#recommendation-next").hidden = totalCount <= limit;
    $("#recommendation-previous").disabled = skip === 0;
    $("#recommendation-next").disabled = skip + limit >= totalCount;
    $("#recommendation-page").textContent =
      `Page ${Math.floor(skip / limit) + 1} of ${Math.ceil(totalCount / limit)} · ${totalCount} movies`;
  }
  $("#match-summary").textContent = showOtherOptions
    ? `${positive} mood matches · ${other} other options. Alternatives may be neutral or lower-ranked.`
    : `${positive} ${positive === 1 ? "match" : "matches"} · ${state.result.mood.name} · Up to ${$("#recommend-form").elements.maximumRuntimeMinutes.value} min`;
  $("#other-options").hidden = !other;
  $("#other-options").textContent = showOtherOptions ? "Show positive matches only" : `Show other options (${other})`;
  if (!results.length) {
    empty(
      "#recommendations",
      other ? "No positive matches for this mood yet" : "No movies fit these filters",
      other ? "Adjust this mood or select Show other options to explore neutral and lower-ranked movies." : "Try a longer runtime, another genre, or include watched movies.",
      "",
      null,
      "search",
    );
    return;
  }
  $("#recommendations").replaceChildren(
    ...results.map((result) => movieCard(result.movie, result)),
  );
}
$("#export").onclick = () =>
  busy($("#export"), async () => {
    if (!requireAccount() || !state.resultPath) return;
    const blob = await api(
      state.resultPath.replace("/recommendations?", "/recommendations.xlsx?"),
      { download: true },
    );
    const url = URL.createObjectURL(blob);
    const anchor = link("", url);
    anchor.download = "MovieWatch-recommendations.xlsx";
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    notice(
      "Excel downloaded for this results page using your current preferences.",
    );
  });

function renderGroups() {
  if (
    signedOutState(
      "#groups",
      "Create a group and find movies that bring everyone’s tastes together.",
    )
  )
    return;
  if (!state.groups.length) {
    empty(
      "#groups",
      "No groups yet",
      "Create a group, add viewers with their profile IDs, and find a movie together.",
      "Create your first group",
      () => editGroup(),
      "users",
    );
    return;
  }
  const expandedGroups = new Set(
    $$("#groups details[open]").map(
      (details) => details.closest("[data-group-id]").dataset.groupId,
    ),
  );
  $("#groups").replaceChildren(
    ...state.groups.map((group) => {
      const owns = group.ownerViewerId === state.viewer.id;
      const manages = owns || state.admin;
      const participating = group.memberships.filter(
        (member) => member.includedInRecommendations,
      ).length;
      const card = el("article", "", "panel group-card");
      card.dataset.groupId = group.id;
      append(
        card,
        append(
          el("div", "", "group-top"),
          icon("users"),
          el("span", owns ? "YOUR GROUP" : "MEMBER", "pill"),
        ),
        el("h2", group.name),
        el("p", group.description || ""),
        el(
          "p",
          `${participating} participating · ${group.memberships.length} ${group.memberships.length === 1 ? "viewer" : "viewers"}`,
        ),
      );
      const details = append(el("details"), el("summary", "View the group"));
      details.open = expandedGroups.has(group.id);
      group.memberships.forEach((member, index) => {
        const isSelf = member.viewerId === state.viewer.id;
        const isOwner = member.viewerId === group.ownerViewerId;
        const row = el("div", "", "member-row");
        const name = isSelf
          ? "You"
          : viewerName(member.viewerId, `Viewer ${index + 1}`);
        append(
          row,
          append(
            el("div"),
            el("strong", `${name}${isOwner ? " · Owner" : ""}`),
            el(
              "small",
              `${member.viewerId.slice(0, 8)} · ${member.includedInRecommendations ? "Participating" : "Sitting out"}`,
            ),
          ),
        );
        const actions = el("div", "", "actions");
        if (isSelf || state.admin)
          actions.append(
            button(
              member.includedInRecommendations ? "Sit out" : "Join in",
              async () => {
                await send(
                  `/api/groups/${group.id}/members/${member.id}`,
                  "PUT",
                  {
                    includedInRecommendations:
                      !member.includedInRecommendations,
                  },
                );
                await refreshGroups();
                notice("Participation updated.");
              },
            ),
          );
        if (!isOwner && (isSelf || manages))
          actions.append(
            button(isSelf ? "Leave" : "Remove", () =>
              confirmDelete(
                isSelf ? `Leave ${group.name}?` : "Remove this viewer?",
                "Their preferences will no longer contribute to this group.",
                async () => {
                  await send(
                    `/api/groups/${group.id}/members/${member.id}`,
                    "DELETE",
                  );
                  await refreshGroups();
                  notice("Group membership updated.");
                },
              ),
            ),
          );
        if (manages && !isOwner)
          actions.append(
            button("Make owner", () =>
              confirmDelete(
                "Transfer group ownership?",
                "This viewer will manage the group. You will remain a member.",
                async () => {
                  await send(`/api/groups/${group.id}/owner`, "PUT", {
                    viewerId: member.viewerId,
                  });
                  await refreshGroups();
                  notice("Group ownership transferred.");
                },
                "Transfer ownership",
              ),
            ),
          );
        row.append(actions);
        details.append(row);
      });
      card.append(details);
      const actions = append(
        el("div", "", "actions"),
        button(
          "Find a movie together",
          () => {
            const form = $("#recommend-form");
            form.elements.scope.value = "group";
            form.elements.groupId.value = group.id;
            syncFinder();
            scheduleRecommendations();
            location.hash = "discover";
            $("#recommend-form").scrollIntoView({ block: "center" });
          },
          "button primary small",
          "sparkles",
        ),
      );
      if (manages)
        append(
          actions,
          button("Add viewer", () => addGroupViewer(group)),
          button("Edit", () => editGroup(group)),
          button(
            "Delete",
            () =>
              confirmDelete(
                `Delete ${group.name}?`,
                "This deletes the group and its memberships for everyone.",
                async () => {
                  await send(`/api/groups/${group.id}`, "DELETE");
                  await refreshGroups();
                  notice("Group deleted.");
                },
              ),
            "button danger small",
          ),
        );
      card.append(actions);
      return card;
    }),
  );
}
function editGroup(group) {
  if (!requireAccount()) return;
  openEdit(
    group ? "Edit your group" : "Create group",
    [
      field("name", "Group name", "text", group?.name || "", null, {
        required: true,
        maxlength: 100,
        placeholder: "Group name",
      }),
      field(
        "description",
        "Description",
        "textarea",
        group?.description || "",
        null,
        { maxlength: 1000 },
      ),
    ],
    async (data) => {
      await send(
        `/api/groups${group ? `/${group.id}` : ""}`,
        group ? "PUT" : "POST",
        data,
      );
      await refreshGroups();
      notice(
        group
          ? "Group updated."
          : "Your group is ready. Add viewers to get everyone together.",
      );
    },
  );
}
$("#create-group").onclick = () => editGroup();

// Shared form dialogs keep editing, validation and error handling consistent.
let editOperation;
function field(
  name,
  label,
  type = "text",
  value = "",
  options,
  attributes = {},
) {
  return { name, label, type, value, options, attributes };
}
function openEdit(
  title,
  fields,
  operation,
  description = "",
  submitLabel = "Save changes",
) {
  editOperation = operation;
  $("#edit-title").textContent = title;
  $("#edit-description").textContent = description;
  $("#edit-submit").textContent = submitLabel;
  $("#edit-form .form-error").textContent = "";
  $("#edit-fields").replaceChildren();
  for (const spec of fields) {
    const label = el("label", spec.label);
    let input;
    if (spec.type === "select") {
      input = el("select");
      spec.options.forEach(([value, text]) =>
        input.add(new Option(text, value)),
      );
    } else if (spec.type === "textarea") input = el("textarea");
    else {
      input = el("input");
      input.type = spec.type;
    }
    input.name = spec.name;
    input.value = spec.value ?? "";
    for (const [name, value] of Object.entries(spec.attributes))
      input.setAttribute(name, value === true ? "" : value);
    label.append(input);
    $("#edit-fields").append(label);
  }
  if (!$("#edit-dialog").open) $("#edit-dialog").showModal();
}
handle("#edit-form", async (_, data) => {
  await editOperation(data);
  $("#edit-dialog").close();
});
function confirmDelete(
  title,
  description,
  operation,
  label = "Confirm removal",
) {
  openEdit(title, [], operation, description, label);
}
function editMovie(movie) {
  openEdit(
    movie ? "Edit movie" : "Add a story to the catalogue",
    [
      field("title", "Title", "text", movie?.title || "", null, {
        required: true,
        maxlength: 200,
      }),
      field("overview", "Overview", "textarea", movie?.overview || "", null, {
        maxlength: 4000,
      }),
      field(
        "runtimeMinutes",
        "Runtime in minutes",
        "number",
        movie?.runtimeMinutes || "",
        null,
        { min: 1 },
      ),
      field("releaseDate", "Release date", "date", movie?.releaseDate || ""),
    ],
    async (data) => {
      const genreIds = [
        ...$("#edit-fields").querySelectorAll("input[name=movieGenre]:checked"),
      ].map((input) => input.value);
      await send(
        `/api/movies${movie ? `/${movie.id}` : ""}`,
        movie ? "PUT" : "POST",
        {
          title: data.title,
          overview: data.overview,
          runtimeMinutes: data.runtimeMinutes
            ? Number(data.runtimeMinutes)
            : null,
          releaseDate: data.releaseDate || null,
          genreIds,
        },
      );
      state.result = null;
      await refreshCatalogue();
      renderRecommendations();
      if ($("#detail-dialog").open) showMovie(movieFor(movie.id));
      notice(movie ? "Movie updated." : "Movie added.");
    },
    movie?.tmdbId
      ? "TMDB imports may overwrite imported metadata. Manually selected genres are kept separately."
      : "A release date and runtime are needed for a movie to appear in recommendations.",
  );
  const genres = append(el("fieldset"), el("legend", "Genres"));
  genres.className = "advanced";
  const choices = el("div", "", "chip-options");
  state.genres.forEach((genre) => {
    const input = el("input");
    input.type = "checkbox";
    input.name = "movieGenre";
    input.value = genre.id;
    input.checked = !!movie?.genres.some((value) => value.id === genre.id);
    choices.append(append(el("label"), input, el("span", genre.name)));
  });
  genres.append(choices);
  $("#edit-fields").append(genres);
}
function editTaxonomy(kind, value) {
  const fields = [
    field("name", "Name", "text", value?.name || "", null, {
      required: true,
      maxlength: 100,
    }),
  ];
  if (kind === "moods")
    fields.push(
      field(
        "description",
        "Description",
        "textarea",
        value?.description || "",
        null,
        { maxlength: 1000 },
      ),
    );
  if (kind === "moods")
    fields.push(field("presetKey", "Starting preset", "select", value?.presetKey || "",
      [["", "Custom — personal preferences only"], ...state.moodPresets.map((p) => [p.key, p.name])]));
  openEdit(
    `${value ? "Edit" : "Add"} ${kind === "genres" ? "genre" : "mood"}`,
    fields,
    async (data) => {
      await send(
        `/api/${kind}${value ? `/${value.id}` : ""}`,
        value ? "PUT" : "POST",
        data,
      );
      await refreshCatalogue();
      notice("Catalogue updated.");
    },
  );
}
function renderAdmin() {
  if (!state.admin) {
    $("#genres").replaceChildren();
    $("#moods").replaceChildren();
    $("#imports").replaceChildren();
    return;
  }
  for (const kind of ["genres", "moods"]) {
    if (!state[kind].length) {
      empty(
        `#${kind}`,
        `No ${kind} yet`,
        `Add ${kind} to help viewers find their next movie.`,
      );
      continue;
    }
    $(`#${kind}`).replaceChildren(
      ...state[kind].map((value) =>
        append(
          el("div", "", "data-row"),
          append(
            el("div"),
            el("strong", value.name),
            value.description ? el("p", value.description) : null,
          ),
          append(
            el("div", "", "actions"),
            button("Edit", () => editTaxonomy(kind, value)),
            button("Delete", () =>
              confirmDelete(
                `Delete ${value.name}?`,
                "Linked movies and preferences may prevent deletion. Remove those links first.",
                async () => {
                  await send(`/api/${kind}/${value.id}`, "DELETE");
                  await refreshCatalogue();
                  notice("Catalogue entry deleted.");
                },
              ),
            ),
          ),
        ),
      ),
    );
  }
  renderImports();
}
function renderImports() {
  if (!state.admin) return;
  if (!state.jobs.length) {
    empty(
      "#imports",
      "No imports yet",
      "Queue an import to bring popular movies into the catalogue.",
      "",
      null,
      "database",
    );
    return;
  }
  $("#imports").replaceChildren(
    ...state.jobs.map((job) => {
      const row = append(
        el("div", "", "data-row"),
        append(
          el("div"),
          el(
            "strong",
            `${job.status} · ${job.pageCount} ${job.pageCount === 1 ? "page" : "pages"}`,
          ),
          el(
            "p",
            `${job.importedCount} imported · ${job.skippedCount} skipped · ${job.attemptCount} attempts`,
          ),
          job.error ? el("p", job.error) : null,
        ),
      );
      const actions = el("div", "", "actions");
      if (job.status === "Pending")
        actions.append(
          button("Edit", () =>
            openEdit(
              "Edit pending import",
              [
                field(
                  "pageCount",
                  "Discovery pages",
                  "number",
                  job.pageCount,
                  null,
                  { min: 1, max: 5, required: true },
                ),
              ],
              async (data) => {
                await send(`/api/import-jobs/${job.id}`, "PUT", {
                  pageCount: Number(data.pageCount),
                  version: job.version,
                });
                await refreshImports();
                notice("Import updated.");
              },
            ),
          ),
        );
      if (job.status !== "Running")
        actions.append(
          button("Delete", () =>
            confirmDelete(
              "Delete this import job?",
              "Imported movies stay in the catalogue.",
              async () => {
                await send(
                  `/api/import-jobs/${job.id}?version=${job.version}`,
                  "DELETE",
                );
                await refreshImports();
                notice("Import job deleted.");
              },
            ),
          ),
        );
      row.append(actions);
      return row;
    }),
  );
}
$("#create-movie").onclick = () => editMovie();
$("#sync-genres").onclick = () =>
  busy($("#sync-genres"), async () => {
    const result = await send("/api/genres/sync", "POST");
    await refreshCatalogue();
    notice(`${result.count} genres synced from TMDB.`);
  });
$("#create-genre").onclick = () => editTaxonomy("genres");
$("#create-mood").onclick = () => editTaxonomy("moods");
handle("#import-form", async (_, data) => {
  await send("/api/import-jobs", "POST", { pageCount: Number(data.pageCount) });
  await refreshImports();
  notice("Import queued. Progress will appear below.");
});
$("#refresh-imports").onclick = () =>
  busy($("#refresh-imports"), async () => {
    await refreshImports();
    await refreshCatalogue();
    notice("Import progress refreshed.");
  });
let polling = false;
setInterval(async () => {
  if (!state.admin || state.route !== "admin" || document.hidden || polling)
    return;
  polling = true;
  try {
    const hadActive = state.jobs.some((job) =>
      ["Pending", "Running"].includes(job.status),
    );
    await refreshImports();
    if (hadActive) await refreshCatalogue();
  } catch (error) {
    notice(error.message, true);
  } finally {
    polling = false;
  }
}, 8000);
handle("#profile-form", async (_, data) => {
  state.viewer = await send("/api/viewers/me", "PUT", data);
  sessionUI();
  notice("Your profile is updated.");
});
$("#copy-id").onclick = () =>
  busy($("#copy-id"), async () => {
    await navigator.clipboard.writeText(state.viewer.id);
    notice("Viewer ID copied. Share it with your group owner.");
  });

function viewerName(id, fallback = "Viewer") {
  return (
    state.viewers.find((viewer) => viewer.id === id)?.displayName || fallback
  );
}
function addGroupViewer(group) {
  const known = state.admin ? state.viewers : [];
  const eligible = known.filter(
    (viewer) =>
      !group.memberships.some((member) => member.viewerId === viewer.id),
  );
  openEdit(
    `Add a viewer to ${group.name}`,
    [
      field("viewerId", "Viewer ID", "text", "", null, {
        required: true,
        pattern: "[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}",
        placeholder: "Paste their viewer ID",
      }),
    ],
    async (data) => {
      await send(`/api/groups/${group.id}/members`, "POST", {
        viewerId: data.viewerId,
      });
      await refreshGroups();
      notice("Viewer added to the group.");
    },
    "Choose a known viewer below, or paste the ID they can copy from My profile.",
  );
  if (eligible.length) {
    const select = el("select");
    select.setAttribute("aria-label", "Choose a known viewer");
    select.add(new Option("Choose a viewer…", ""));
    eligible.forEach((viewer) =>
      select.add(new Option(viewer.displayName, viewer.id)),
    );
    select.onchange = () => {
      $("#edit-fields [name=viewerId]").value = select.value;
    };
    $("#edit-fields").prepend(append(el("label", "Known viewers"), select));
  }
}
function renderViewers() {
  if (!state.admin) {
    $("#viewers").replaceChildren();
    return;
  }
  $("#viewers").replaceChildren(
    ...state.viewers.map((viewer) =>
      append(
        el("div", "", "data-row"),
        append(
          el("div"),
          el("strong", viewer.displayName),
          el("p", viewer.email),
          el("p", viewer.id),
        ),
        append(
          el("div", "", "actions"),
          button("Copy ID", async () => {
            await navigator.clipboard.writeText(viewer.id);
            notice("Viewer ID copied.");
          }),
          button("Edit", () => editViewer(viewer)),
          button(
            "Delete",
            () =>
              confirmDelete(
                `Delete ${viewer.displayName}?`,
                "This removes their account and personal data. Owned groups must be transferred or deleted first.",
                async () => {
                  await send(`/api/viewers/${viewer.id}`, "DELETE");
                  if (viewer.id === state.viewer.id) signOut();
                  else {
                    await refreshViewers();
                    await refreshGroups();
                  }
                  notice("Viewer deleted.");
                },
              ),
            "button danger small",
          ),
        ),
      ),
    ),
  );
}
function editViewer(viewer) {
  const fields = [
    field(
      "displayName",
      "Display name",
      "text",
      viewer?.displayName || "",
      null,
      { required: true, maxlength: 100 },
    ),
    field("email", "Email", "email", viewer?.email || "", null, {
      required: true,
    }),
  ];
  if (!viewer)
    fields.push(
      field("password", "Password", "password", "", null, {
        required: true,
        minlength: 8,
        autocomplete: "new-password",
      }),
    );
  openEdit(
    viewer ? "Edit viewer" : "Add viewer",
    fields,
    async (data) => {
      const result = await send(
        `/api/viewers${viewer ? `/${viewer.id}` : ""}`,
        viewer ? "PUT" : "POST",
        data,
      );
      if (viewer?.id === state.viewer.id) {
        state.viewer = result;
        sessionUI();
      }
      await refreshViewers();
      notice(
        viewer
          ? "Viewer updated."
          : "Viewer created. They can sign in with the email and password you chose.",
      );
    },
    viewer
      ? "Update this viewer’s profile."
      : "Use at least 8 characters, including uppercase, lowercase, a number, and a symbol. New accounts have the Viewer role.",
  );
}
$("#create-viewer").onclick = () => editViewer();
$("#delete-account").onclick = () => {
  if (!requireAccount()) return;
  openEdit(
    "Delete your account?",
    [
      field("confirmation", "Type DELETE to confirm", "text", "", null, {
        required: true,
        pattern: "DELETE",
        autocomplete: "off",
      }),
    ],
    async () => {
      await send("/api/viewers/me", "DELETE");
      signOut();
      location.hash = "discover";
      notice("Your account has been deleted.");
    },
    "This removes your preferences, watchlist, and group memberships. Transfer or delete groups you own first.",
    "Delete my account",
  );
};
async function loadEnvironment() {
  try {
    state.environment = await api("/api/status");
    renderTmdbSetup();
  } catch (error) {
    notice(`Could not check app setup: ${error.message}`, true);
  }
}
function renderSetup() {
  const target = $("#setup-status");
  target.replaceChildren();
  if (!state.viewer || state.loading) {
    target.hidden = true;
    return;
  }
  const missing = [
    !state.moods.length && "moods",
    !state.genres.length && "genres",
    !state.movies.length && "movies",
  ].filter(Boolean);
  target.hidden = !missing.length && !state.catalogueError;
  if (target.hidden) return;
  append(
    target,
    icon("database"),
    append(
      el("div"),
      el(
        "strong",
        state.catalogueError
          ? "Could not load the catalogue"
          : `Your catalogue needs ${missing.join(", ")}`,
      ),
      el(
        "p",
        state.catalogueError ||
          (state.admin
            ? "Open Manage to sync genres, add moods, and import movies from TMDB."
            : "Ask an administrator to add the missing catalogue data."),
      ),
      append(
        el("div", "", "actions"),
        state.admin ? link("Manage", "#admin") : null,
        button("Retry loading", refreshCatalogue),
      ),
    ),
  );
}
function renderTmdbSetup() {
  const target = $("#tmdb-setup");
  target.replaceChildren();
  append(
    target,
    el(
      "strong",
      state.environment.tmdbConfigured
        ? "Read Access Token is configured"
        : "TMDB Read Access Token is missing",
    ),
    el(
      "p",
      state.environment.importWorkerEnabled === false
        ? "The import worker is disabled. Queued jobs will stay pending until it is enabled and the app restarts."
        : "Queue one page to verify the connection. The job will report imported movies or a clear error.",
    ),
  );
  if (!state.environment.tmdbConfigured) {
    target.append(
      link(
        "Get a TMDB Read Access Token ↗",
        "https://www.themoviedb.org/settings/api",
      ),
    );
    target.append(
      el(
        "pre",
        'dotnet user-secrets set "Tmdb:ReadAccessToken" "YOUR_READ_ACCESS_TOKEN" --project MovieWatch.Web',
        "command",
      ),
    );
    target.append(
      el(
        "p",
        "Save the token in the same Windows or WSL environment where you run the app, then restart it. The token is never shown in this UI.",
        "form-hint",
      ),
    );
  }
}

sessionUI();
fillControls();
renderAll();
navigate();
loadEnvironment().then(bootstrap);
