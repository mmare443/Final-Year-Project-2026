/* Production API origin. Loaded after contactConfig so it wins over any
   cached localhost value. Public pages must not call localhost.
   PRODUCTION_API_ORIGIN is declared only here. */
window.PRODUCTION_API_ORIGIN = "http://api.lccbportal.org";
window.API_ORIGIN = window.PRODUCTION_API_ORIGIN;

window.collegeApiUrl = function collegeApiUrl(path) {
    var origin = window.PRODUCTION_API_ORIGIN || window.API_ORIGIN || "http://api.lccbportal.org";
    if (/localhost|127\.0\.0\.1/i.test(String(origin))) {
        origin = window.PRODUCTION_API_ORIGIN;
    }
    return String(origin).replace(/\/$/, "") + path;
};
