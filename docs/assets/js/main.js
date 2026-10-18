/**
 * DJI LUT Studio - Showcase & Landing Page Interactive Controller
 * Native JavaScript (Zero heavy dependencies)
 */

document.addEventListener('DOMContentLoaded', () => {
  initComparisonSlider();
  initGalleryTabs();
  initCatalogExplorer();
  initCopyChecksum();
  initMobileNav();
});

/* =====================================================================
   1. Interactive Before / After Comparison Slider
   ===================================================================== */
function initComparisonSlider() {
  const box = document.getElementById('comparisonBox');
  if (!box) return;

  let isDragging = false;

  function updateSliderPosition(clientX) {
    const rect = box.getBoundingClientRect();
    let x = clientX - rect.left;
    if (x < 0) x = 0;
    if (x > rect.width) x = rect.width;
    const percentage = ((x / rect.width) * 100).toFixed(2);
    box.style.setProperty('--slider-pos', `${percentage}%`);
  }

  // Pointer events (handles Mouse & Touch uniformly)
  box.addEventListener('pointerdown', (e) => {
    isDragging = true;
    box.setPointerCapture(e.pointerId);
    updateSliderPosition(e.clientX);
  });

  box.addEventListener('pointermove', (e) => {
    if (!isDragging) return;
    updateSliderPosition(e.clientX);
  });

  box.addEventListener('pointerup', (e) => {
    isDragging = false;
    try { box.releasePointerCapture(e.pointerId); } catch (_) {}
  });

  box.addEventListener('pointercancel', () => {
    isDragging = false;
  });

  // Keyboard accessibility
  box.setAttribute('tabindex', '0');
  box.setAttribute('role', 'slider');
  box.setAttribute('aria-label', 'Before and After Image Comparison Slider');
  box.setAttribute('aria-valuenow', '50');

  box.addEventListener('keydown', (e) => {
    const current = parseFloat(getComputedStyle(box).getPropertyValue('--slider-pos')) || 50;
    if (e.key === 'ArrowLeft') {
      const next = Math.max(0, current - 5);
      box.style.setProperty('--slider-pos', `${next}%`);
      box.setAttribute('aria-valuenow', next.toString());
    } else if (e.key === 'ArrowRight') {
      const next = Math.min(100, current + 5);
      box.style.setProperty('--slider-pos', `${next}%`);
      box.setAttribute('aria-valuenow', next.toString());
    }
  });
}

/* =====================================================================
   2. Screenshot Gallery Tab Controller
   ===================================================================== */
function initGalleryTabs() {
  const tabs = document.querySelectorAll('.gallery-tabs .tab-btn');
  const frame = document.getElementById('galleryFrame');
  const title = document.getElementById('galleryCaptionTitle');
  const desc = document.getElementById('galleryCaptionDesc');

  if (!tabs.length || !frame) return;

  const galleryData = {
    'gui': {
      src: 'assets/images/app-gui.png',
      title: '🖥️ Native Windows Desktop GUI (WPF .NET 4.8)',
      desc: 'Sleek dark-themed interface with real-time multi-GPU encoding telemetry, live progress indicators, and instant side-by-side verification.'
    },
    'gyroflow': {
      src: 'assets/images/gyroflow-integration.png',
      title: '🎯 Intelligent EIS Detection & Gyroflow Telemetry Bridge',
      desc: 'Automatically reads in-camera RockSteady & HorizonSteady tags to advise on gyro motion telemetry before 10-bit H.265 hardware stabilization.'
    },
    'controls': {
      src: 'assets/images/app-controls.png',
      title: '🎛️ Intelligent Camera Auto-Matching & Fine-Tuning Controls',
      desc: 'Automatic camera profile detection with target resolution scalers (4K, 2.7K, 1080p), CQ quality factors, and artistic creative look overlays.'
    },
    'preview': {
      src: 'assets/images/split-preview.jpg',
      title: '⚖️ Side-by-Side Quality Assurance Previews',
      desc: 'Automatically exports a high-resolution split-screen preview JPEG (_preview.jpg) alongside every graded video for immediate verification.'
    }
  };

  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      tabs.forEach(t => t.classList.remove('active'));
      tab.classList.add('active');

      const key = tab.getAttribute('data-tab');
      const item = galleryData[key];
      if (item) {
        frame.style.opacity = '0.3';
        setTimeout(() => {
          frame.src = item.src;
          if (title) title.textContent = item.title;
          if (desc) desc.textContent = item.desc;
          frame.style.opacity = '1';
        }, 120);
      }
    });
  });
}

/* =====================================================================
   3. Camera & LUT Explorer Catalog
   ===================================================================== */
const CATALOG_ITEMS = [
  // Drones
  { cat: 'drones', name: 'DJI O4 Pro / Air Unit', log: 'D-Log M', luts: ['Official O4 D-Log M to Rec.709 V1'], gyro: true },
  { cat: 'drones', name: 'DJI Avata 2', log: 'D-Log M', luts: ['Official Avata 2 D-Log M to Rec.709 V1'], gyro: true },
  { cat: 'drones', name: 'DJI Avata 360', log: 'D-Log M', luts: ['Official Avata 360 D-Log M to Rec.709'], gyro: true },
  { cat: 'drones', name: 'DJI Mavic 3 / Pro / Cine', log: 'D-Log & D-Log M', luts: ['Official D-Log to Rec.709', 'Official D-Log to Rec.709 Vivid', 'Official D-Log M to Rec.709'], gyro: false },
  { cat: 'drones', name: 'DJI Mavic 4 Pro', log: 'D-Log & D-Log M', luts: ['Official D-Log to Rec.709 Standard', 'Official D-Log to Rec.709 Vivid', 'Official D-Log M to Rec.709'], gyro: false },
  { cat: 'drones', name: 'DJI Air 3 / Air 3S', log: 'D-Log M', luts: ['Official Air 3 D-Log M to Rec.709', 'Official Air 3S D-Log M to Rec.709'], gyro: false },
  { cat: 'drones', name: 'DJI Mini 4 Pro / Mini 5 Pro', log: 'D-Log M', luts: ['Official Mini 4 Pro D-Log M to Rec.709', 'Official Mini 5 Pro D-Log M to Rec.709'], gyro: false },
  { cat: 'drones', name: 'DJI Flip', log: 'D-Log M', luts: ['Official DJI Flip D-Log M to Rec.709'], gyro: false },

  // Action & Pocket
  { cat: 'action', name: 'DJI Osmo Pocket 3', log: 'D-Log M', luts: ['Official D-Log M to Rec.709', 'Spring Pro Look', 'Summer Pro Look', 'Autumn Pro Look', 'Winter Pro Look'], gyro: false },
  { cat: 'action', name: 'DJI Osmo Pocket 4 / 4 Pro', log: 'D-Log & D-Log2', luts: ['Pocket 4 D-Log to Rec.709', 'Pocket 4 Pro D-Log2 to Rec.709 Standard', 'Pocket 4 Pro D-Log2 Vivid', 'Pocket 4 D-Log M'], gyro: false },
  { cat: 'action', name: 'DJI Osmo Action 4', log: 'D-Log M', luts: ['Official D-Log M to Rec.709', 'Action Ice Pro Look', 'Action Nature Pro Look', 'Action Forest Pro Look'], gyro: true },
  { cat: 'action', name: 'DJI Osmo Action 5 Pro', log: 'D-Log M', luts: ['Official D-Log M to Rec.709', 'Master Look Mei (Plum)', 'Master Look Ju (Chrysanthemum)', 'Master Look Zhu (Bamboo)', 'Master Look Lan (Orchid)'], gyro: true },
  { cat: 'action', name: 'DJI Osmo Action 6', log: 'D-Log M', luts: ['Official Action 6 D-Log M to Rec.709'], gyro: true },
  { cat: 'action', name: 'DJI Lito / Lito X1', log: 'D-Log M', luts: ['Official Lito X1 D-Log M to Rec.709'], gyro: false },
  { cat: 'action', name: 'DJI Osmo Nano', log: 'D-Log M', luts: ['Official Osmo Nano D-Log M to Rec.709'], gyro: true },
  { cat: 'action', name: 'DJI Osmo 360', log: 'D-Log M', luts: ['Official Osmo 360 D-Log M to Rec.709'], gyro: true },

  // Cinema & Enterprise
  { cat: 'cinema', name: 'DJI Zenmuse X7 / Ronin 4D', log: 'Cinema D-Log', luts: ['Official Cinema D-Log to Rec.709 Standard', 'Linear to D-Log EI Mode'], gyro: false },
  { cat: 'cinema', name: 'DJI Phantom 4 Pro', log: 'D-Log', luts: ['Official Phantom 4 D-Log to Rec.709', 'Phantom 4 sRGB Booster'], gyro: false },
  { cat: 'cinema', name: 'DJI Generic / Universal Profile', log: 'Universal D-Log M', luts: ['Universal DJI D-Log M to Rec.709 Reference V1'], gyro: false },

  // Creative Looks
  { cat: 'creative', name: 'Artistic Cinematic Master Looks', log: 'Post-LUT Style', luts: ['Vibrant Pop (Warm Punch)', 'Cinematic 35mm Film Emulation', 'Golden Hour Sunset Glow', 'Teal & Orange Hollywood Grade', 'Moody Noir High-Contrast B&W', 'Clean Commercial Vivid', 'D-Cinelike Natural Booster'], gyro: false }
];

function initCatalogExplorer() {
  const grid = document.getElementById('catalogGrid');
  const searchInput = document.getElementById('catalogSearch');
  const chips = document.querySelectorAll('.category-chips .chip-btn');
  const countBadge = document.getElementById('catalogCount');

  if (!grid) return;

  let activeCategory = 'all';
  let searchTerm = '';

  function render() {
    grid.innerHTML = '';
    const filtered = CATALOG_ITEMS.filter(item => {
      const matchCat = activeCategory === 'all' || item.cat === activeCategory;
      const term = searchTerm.toLowerCase().trim();
      const matchSearch = !term || 
        item.name.toLowerCase().includes(term) ||
        item.log.toLowerCase().includes(term) ||
        item.luts.some(l => l.toLowerCase().includes(term));
      return matchCat && matchSearch;
    });

    if (countBadge) {
      countBadge.textContent = `${filtered.length} Models & Profiles`;
    }

    if (filtered.length === 0) {
      grid.innerHTML = `
        <div style="grid-column: 1 / -1; text-align: center; padding: 48px 20px; color: var(--text-dim);">
          <div style="font-size: 2rem; margin-bottom: 8px;">🔍</div>
          <div style="font-size: 1.1rem; color: #fff; margin-bottom: 4px;">No matching cameras or LUTs found</div>
          <p>Try searching for "Avata", "O4", "Action 5", "Pocket 3", or "Mavic".</p>
        </div>
      `;
      return;
    }

    filtered.forEach(item => {
      const card = document.createElement('div');
      card.className = 'device-card';

      const catLabel = item.cat === 'drones' ? 'Drone / Air Unit' :
                       item.cat === 'action' ? 'Action / Handheld' :
                       item.cat === 'cinema' ? 'Cinema & Enterprise' : 'Creative Looks';

      card.innerHTML = `
        <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 6px;">
          <span class="device-category-tag">${catLabel}</span>
          ${item.gyro ? '<span class="pill pill-green" style="font-size: 0.68rem; padding: 2px 6px;">🎯 Gyroflow Ready</span>' : ''}
        </div>
        <h4 class="device-name">${item.name}</h4>
        <div style="font-size: 0.8rem; color: var(--text-dim); margin-bottom: 10px;">
          Color Space: <strong style="color: var(--text-muted);">${item.log}</strong>
        </div>
        <ul class="device-lut-list">
          ${item.luts.map(lut => `
            <li class="device-lut-item">
              <span class="device-lut-bullet">▸</span>
              <span>${lut}</span>
            </li>
          `).join('')}
        </ul>
      `;
      grid.appendChild(card);
    });
  }

  // Chips click
  chips.forEach(chip => {
    chip.addEventListener('click', () => {
      chips.forEach(c => c.classList.remove('active'));
      chip.classList.add('active');
      activeCategory = chip.getAttribute('data-cat');
      render();
    });
  });

  // Search input
  if (searchInput) {
    searchInput.addEventListener('input', (e) => {
      searchTerm = e.target.value;
      render();
    });
  }

  render();
}

/* =====================================================================
   4. Copy Checksum to Clipboard with Toast Notification
   ===================================================================== */
function initCopyChecksum() {
  const btn = document.getElementById('btnCopyChecksum');
  const code = document.getElementById('checksumCode');
  const toast = document.getElementById('toast');

  if (!btn || !code) return;

  btn.addEventListener('click', () => {
    const text = code.textContent.trim();
    navigator.clipboard.writeText(text).then(() => {
      btn.textContent = '✓ Copied!';
      showToast('SHA-256 Checksum copied to clipboard!');
      setTimeout(() => {
        btn.textContent = 'Copy Hash';
      }, 2500);
    }).catch(() => {
      showToast('Checksum: ' + text);
    });
  });
}

function showToast(msg) {
  let toast = document.getElementById('toast');
  if (!toast) {
    toast = document.createElement('div');
    toast.id = 'toast';
    toast.className = 'toast';
    document.body.appendChild(toast);
  }
  toast.textContent = msg;
  toast.classList.add('show');
  setTimeout(() => {
    toast.classList.remove('show');
  }, 3000);
}

/* =====================================================================
   5. Mobile Navigation
   ===================================================================== */
function initMobileNav() {
  const toggle = document.getElementById('mobileToggle');
  const nav = document.getElementById('navLinks');

  if (!toggle || !nav) return;

  toggle.addEventListener('click', () => {
    nav.classList.toggle('mobile-open');
    toggle.textContent = nav.classList.contains('mobile-open') ? '✕' : '☰';
  });

  nav.querySelectorAll('.nav-link').forEach(link => {
    link.addEventListener('click', () => {
      nav.classList.remove('mobile-open');
      toggle.textContent = '☰';
    });
  });
}
