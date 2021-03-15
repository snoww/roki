const sections = document.querySelectorAll(".settings");
const nav = document.querySelectorAll(".nav-settings");
window.addEventListener("scroll", () => {
    let current = "";
    sections.forEach(section => {
        const sectionTop = section.offsetTop;
        const sectionHeight = section.clientHeight;
        if (pageYOffset >= (sectionTop - sectionHeight / 2)){
            current = section.getAttribute("id");
        }
    });
    nav.forEach(n => {
        n.classList.remove("sidenav-active");
        if (n.classList.contains(current)){
            n.classList.add("sidenav-active");
        }
    })
});

// enable/disable save button when there are changes detected
const coreForm = document.getElementById("form-data-core");
const currForm = document.getElementById("form-data-curr");
const xpForm = document.getElementById("form-data-xp");
const gamesForm = document.getElementById("form-data-games");
const gambleForm = document.getElementById("form-data-gamble");
const coreData = Object.fromEntries(new FormData(coreForm).entries());
const currData = Object.fromEntries(new FormData(currForm).entries());
const xpData = Object.fromEntries(new FormData(xpForm).entries());
const gamesData = Object.fromEntries(new FormData(gamesForm).entries());
const gambleData = Object.fromEntries(new FormData(gambleForm).entries());

function checkForm(oldData, id, saveId) {
    const newData = Object.fromEntries(new FormData(document.getElementById(id)).entries());
    document.getElementById(saveId).disabled = _.isEqual(oldData, newData);
}

coreForm.addEventListener("keyup", () => checkForm(coreData, "form-data-core", "save-form-core"));
currForm.addEventListener("keyup", () => checkForm(currData, "form-data-curr", "save-form-curr"));
xpForm.addEventListener("keyup", () => checkForm(xpData, "form-data-xp", "save-form-xp"));
gamesForm.addEventListener("keyup", () => checkForm(gamesData, "form-data-games", "save-form-games"));
gambleForm.addEventListener("keyup", () => checkForm(gambleData, "form-data-gamble", "save-form-gamble"));