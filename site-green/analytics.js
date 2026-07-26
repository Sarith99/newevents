// ---- New Event analytics tracking ----
(function () {
  // Anonymous per-visit session ID (no personal data)
  const sessionId = 'sess-' + Math.random().toString(36).substring(2) + Date.now();

  function track(eventType, eventDetail) {
    fetch('/api/track', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        eventType: eventType,
        eventDetail: eventDetail,
        sessionId: sessionId,
        pageUrl: window.location.pathname
      })
    }).catch(function () { /* never break the site */ });
  }

  document.addEventListener('DOMContentLoaded', function () {

    // ---- METRIC 1: Registration funnel ----
    track('funnel_step', 'page_loaded');                       // entered

    // Clicked a "Register" call-to-action button
    document.querySelectorAll('a[href="#register"]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        track('funnel_step', 'register_cta_clicked');           // showed intent
      });
    });

    const registerSection = document.getElementById('register');
    if (registerSection) {
      // Started filling the form (first focus on any field)
      let started = false;
      registerSection.addEventListener('focusin', function () {
        if (!started) { started = true; track('funnel_step', 'form_started'); }
      });
      // Submitted the form
      const form = registerSection.querySelector('form');
      if (form) {
        form.addEventListener('submit', function () {
          track('funnel_step', 'form_submitted');               // converted
        });
      }
    }

    // ---- METRIC 2: Section engagement ----
    const sectionIds = ['intro','overview','detail','video','speakers','program','register','venue','sponsors','contact'];
    const seen = new Set();
    if ('IntersectionObserver' in window) {
      const observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting && !seen.has(entry.target.id)) {
            seen.add(entry.target.id);
            track('section_view', entry.target.id);
          }
        });
      }, { threshold: 0.4 });
      sectionIds.forEach(function (id) {
        const el = document.getElementById(id);
        if (el) observer.observe(el);
      });
    }

    // ---- METRIC 3: Video engagement ----
    // The video is a YouTube embed, so we track engagement with the video section:
    // scrolling it into view, and clicking on/near the player.
    const videoSection = document.getElementById('video');
    if (videoSection) {
      if ('IntersectionObserver' in window) {
        let videoSeen = false;
        const vObserver = new IntersectionObserver(function (entries) {
          entries.forEach(function (entry) {
            if (entry.isIntersecting && !videoSeen) {
              videoSeen = true;
              track('video_play', 'video_section_viewed');
            }
          });
        }, { threshold: 0.6 });
        vObserver.observe(videoSection);
      }
      // Clicking on the video area (a proxy for pressing play on the embed)
      videoSection.addEventListener('click', function () {
        track('video_play', 'video_clicked');
      });
    }

  });
})();