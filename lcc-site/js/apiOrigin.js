/* Production API origin. Loaded after contactConfig so it wins over any
   cached localhost value. Public pages must not call localhost.
   PRODUCTION_API_ORIGIN is declared only here. */
window.PRODUCTION_API_ORIGIN = "https://api.lccbportal.org";
window.API_ORIGIN = window.PRODUCTION_API_ORIGIN;

window.collegeApiUrl = function collegeApiUrl(path) {
    var pageHost = window.location && window.location.hostname;
    var origin = (pageHost === "localhost" || pageHost === "127.0.0.1")
        ? "http://localhost:5080"
        : (window.PRODUCTION_API_ORIGIN || "https://api.lccbportal.org");
    return String(origin).replace(/\/$/, "") + path;
};
