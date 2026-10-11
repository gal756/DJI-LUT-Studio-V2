/**
 * DJI LUT Studio - Frontend Application Controller
 */

let state = {
  categories: [],
  devices: [],
  luts: [],
  selectedCategory: 'all',
  selectedDevice: null,
  selectedLut: null,
  selectedLook: 'natural',
  scannedClips: [],
  selectedClip: null,
  jobPollingTimer: null,
};

// DOM Elements
const el = {
  gpuBadge: document.getElementById('gpuBadge'),
  gpuText: document.getElementById('gpuText'),
  lutCountText: document.getElementById('lutCountText'),
  categoryTabs: document.getElementById('categoryTabs'),
  deviceGrid: document.getElementById('deviceGrid'),
  lutList: document.getElementById('lutList'),
  currentDeviceName: document.getElementById('currentDeviceName'),
  lookPills: document.getElementById('lookPills'),
  inputPath: document.getElementById('inputPath'),
  btnScan: document.getElementById('btnScan'),
  emptyState: document.getElementById('emptyState'),
  clipsContainer: document.getElementById('clipsContainer'),
  clipsList: document.getElementById('clipsList'),
  btnGeneratePreview: document.getElementById('btnGeneratePreview'),
  previewDisplay: document.getElementById('previewDisplay'),
  previewImg: document.getElementById('previewImg'),
  selectCodec: document.getElementById('selectCodec'),
  selectScale: document.getElementById('selectScale'),
  cqRange: document.getElementById('cqRange'),
  cqVal: document.getElementById('cqVal'),
  chkSharpen: document.getElementById('chkSharpen'),
  btnStartGrade: document.getElementById('btnStartGrade'),
  progressPanel: document.getElementById('progressPanel'),
  progVideoName: document.getElementById('progVideoName'),
  progSpeed: document.getElementById('progSpeed'),
  progEta: document.getElementById('progEta'),
  progFill: document.getElementById('progFill'),
  progPercent: document.getElementById('progPercent'),
};

// Initialize Application
async function init() {
  setupEventListeners();
  await fetchSystemInfo();
  await fetchManifest();
  startStatusPolling();
}

function setupEventListeners() {
  // Category tabs
  el.categoryTabs.addEventListener('click', (e) => {
    const btn = e.target.closest('.tab-btn');
    if (!btn) return;
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    state.selectedCategory = btn.dataset.cat;
    renderDevices();
  });

  // Look styles
  el.lookPills.addEventListener('click', (e) => {
    const pill = e.target.closest('.look-pill');
    if (!pill) return;
    document.querySelectorAll('.look-pill').forEach(p => p.classList.remove('active'));
    pill.classList.add('active');
    state.selectedLook = pill.dataset.look;
  });

  // CQ Slider
  el.cqRange.addEventListener('input', (e) => {
    el.cqVal.textContent = e.target.value;
  });

  // Scan folder
  el.btnScan.addEventListener('click', scanFolder);
  el.inputPath.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') scanFolder();
  });

  // Preview Generation
  el.btnGeneratePreview.addEventListener('click', generatePreview);

  // Start Grade
  el.btnStartGrade.addEventListener('click', startGrading);
}

// Fetch System Info
async function fetchSystemInfo() {
  try {
    const res = await fetch('/api/system');
    const data = await res.json();
    if (data.nvenc_available) {
      el.gpuText.textContent = `RTX NVENC Active (10-bit)`;
      el.gpuBadge.classList.add('gpu-badge');
    } else {
      el.gpuText.textContent = `CPU Mode (${data.gpu_name})`;
      el.gpuBadge.classList.remove('gpu-badge');
    }
    el.lutCountText.textContent = `${data.lut_count} 3D LUTs Ready`;
  } catch (err) {
    el.gpuText.textContent = 'API Offline';
  }
}

// Fetch LUT Manifest
async function fetchManifest() {
  try {
    const res = await fetch('/api/manifest');
    const data = await res.json();
    state.categories = data.categories || [];
    state.devices = data.devices || [];
    state.luts = data.luts || [];

    renderDevices();

    // Select first device by default (e.g. O4 Pro)
    if (state.devices.length > 0) {
      selectDevice(state.devices[0].id);
    }
  } catch (err) {
    console.error('Failed to load manifest:', err);
  }
}

// Render Devices
function renderDevices() {
  el.deviceGrid.innerHTML = '';
  const filtered = state.devices.filter(dev => {
    if (state.selectedCategory === 'all') return true;
    return dev.category_id === state.selectedCategory;
  });

  filtered.forEach(dev => {
    const card = document.createElement('div');
    card.className = `device-card ${state.selectedDevice === dev.id ? 'active' : ''}`;
    card.dataset.id = dev.id;
    card.innerHTML = `
      <div class="device-title">${dev.name}</div>
      <div class="device-sub">${dev.luts ? dev.luts.length : 0} LUTs available</div>
    `;
    card.addEventListener('click', () => selectDevice(dev.id));
    el.deviceGrid.appendChild(card);
  });
}

// Select Device
function selectDevice(devId) {
  state.selectedDevice = devId;
  const dev = state.devices.find(d => d.id === devId);
  if (!dev) return;

  el.currentDeviceName.textContent = dev.name;
  document.querySelectorAll('.device-card').forEach(c => {
    c.classList.toggle('active', c.dataset.id === devId);
  });

  // Populate LUTs for selected device
  renderLutsForDevice(dev);
}

// Render LUTs for Device
function renderLutsForDevice(dev) {
  el.lutList.innerHTML = '';
  const luts = dev.luts || [];

  if (luts.length === 0) {
    el.lutList.innerHTML = '<div class="empty-state"><p>No LUTs configured for this device.</p></div>';
    return;
  }

  // Pre-select recommended or first LUT
  let defaultLut = luts.find(l => l.recommended) || luts[0];
  state.selectedLut = defaultLut.id;

  luts.forEach(lut => {
    const item = document.createElement('div');
    item.className = `lut-item ${state.selectedLut === lut.id ? 'active' : ''}`;
    item.dataset.id = lut.id;
    item.innerHTML = `
      <div class="lut-info">
        <div class="lut-title">${lut.name}</div>
        <div class="lut-desc">${lut.description || ''}</div>
      </div>
      ${lut.recommended ? '<span class="lut-badge-rec">Recommended</span>' : ''}
    `;
    item.addEventListener('click', () => {
      document.querySelectorAll('.lut-item').forEach(i => i.classList.remove('active'));
      item.classList.add('active');
      state.selectedLut = lut.id;
    });
    el.lutList.appendChild(item);
  });
}

// Scan Folder
async function scanFolder() {
  const path = el.inputPath.value.trim();
  if (!path) return;

  el.btnScan.disabled = true;
  el.btnScan.textContent = 'Scanning...';

  try {
    const res = await fetch('/api/scan', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path }),
    });
    const data = await res.json();
    if (data.error) {
      alert(`Scan error: ${data.error}`);
      return;
    }

    state.scannedClips = data.clips || [];
    renderClips();
  } catch (err) {
    alert(`Failed to scan: ${err}`);
  } finally {
    el.btnScan.disabled = false;
    el.btnScan.innerHTML = `
      <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2" fill="none"><circle cx="11" cy="1" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
      Scan Folder
    `;
  }
}

// Render Video Clips
function renderClips() {
  if (state.scannedClips.length === 0) {
    el.emptyState.style.display = 'flex';
    el.clipsList.style.display = 'none';
    return;
  }

  el.emptyState.style.display = 'none';
  el.clipsList.style.display = 'flex';
  el.clipsList.innerHTML = '';

  state.scannedClips.forEach((clip, idx) => {
    const row = document.createElement('div');
    row.className = `clip-row ${idx === 0 ? 'active' : ''}`;
    row.dataset.path = clip.path;

    const dur = clip.duration ? `${Math.round(clip.duration)}s` : '--';
    row.innerHTML = `
      <div class="clip-info">
        <div class="clip-name">${clip.filename}</div>
        <div class="clip-meta">${clip.resolution} @ ${clip.fps}fps | ${dur} | Auto: ${clip.detected_lut_name || 'Generic'}</div>
      </div>
    `;

    row.addEventListener('click', () => {
      document.querySelectorAll('.clip-row').forEach(r => r.classList.remove('active'));
      row.classList.add('active');
      state.selectedClip = clip;

      // Auto-switch device if detected
      if (clip.detected_device) {
        selectDevice(clip.detected_device);
      }
      if (clip.detected_lut) {
        state.selectedLut = clip.detected_lut;
        document.querySelectorAll('.lut-item').forEach(i => {
          i.classList.toggle('active', i.dataset.id === clip.detected_lut);
        });
      }
    });

    el.clipsList.appendChild(row);
  });

  // Select first clip
  state.selectedClip = state.scannedClips[0];
  if (state.selectedClip.detected_device) {
    selectDevice(state.selectedClip.detected_device);
  }
}

// Generate Comparison Preview
async function generatePreview() {
  if (!state.selectedClip) {
    alert('Please scan and select a video clip first.');
    return;
  }
  if (!state.selectedLut) {
    alert('Please select a LUT from the library.');
    return;
  }

  el.btnGeneratePreview.disabled = true;
  el.btnGeneratePreview.textContent = 'Generating...';

  try {
    const res = await fetch('/api/preview', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        video_path: state.selectedClip.path,
        lut_id: state.selectedLut,
        look: state.selectedLook,
      }),
    });
    const data = await res.json();
    if (data.success && data.preview_url) {
      el.previewImg.src = `${data.preview_url}?t=${Date.now()}`;
      el.previewImg.style.display = 'block';
      const placeholder = el.previewDisplay.querySelector('.preview-placeholder');
      if (placeholder) placeholder.style.display = 'none';
    } else {
      alert(`Preview failed: ${data.error || 'Unknown error'}`);
    }
  } catch (err) {
    alert(`Preview error: ${err}`);
  } finally {
    el.btnGeneratePreview.disabled = false;
    el.btnGeneratePreview.innerHTML = `
      <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle></svg>
      Generate Preview Snapshot
    `;
  }
}

// Start Grading
async function startGrading() {
  if (!state.selectedClip) {
    alert('Please select a video clip to grade.');
    return;
  }
  if (!state.selectedLut) {
    alert('Please select a LUT.');
    return;
  }

  const payload = {
    video_path: state.selectedClip.path,
    lut_id: state.selectedLut,
    look: state.selectedLook,
    codec: el.selectCodec.value,
    cq: parseInt(el.cqRange.value, 10),
    scale: el.selectScale.value,
    sharpen: el.chkSharpen.checked,
  };

  el.btnStartGrade.disabled = true;
  el.btnStartGrade.textContent = 'Starting GPU Encoder...';

  try {
    const res = await fetch('/api/grade', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const data = await res.json();
    if (data.error) {
      alert(`Grading error: ${data.error}`);
      el.btnStartGrade.disabled = false;
      el.btnStartGrade.textContent = 'Apply LUT & Grade Selected Video';
    } else {
      el.progressPanel.style.display = 'flex';
    }
  } catch (err) {
    alert(`Failed to start job: ${err}`);
    el.btnStartGrade.disabled = false;
    el.btnStartGrade.textContent = 'Apply LUT & Grade Selected Video';
  }
}

// Status Polling Loop
function startStatusPolling() {
  if (state.jobPollingTimer) clearInterval(state.jobPollingTimer);
  state.jobPollingTimer = setInterval(async () => {
    try {
      const res = await fetch('/api/status');
      const data = await res.json();

      if (data.status === 'encoding') {
        el.progressPanel.style.display = 'flex';
        el.progVideoName.textContent = data.video || 'Encoding';
        el.progSpeed.textContent = `${data.speed || '1.0x'} (${data.fps || 0} fps)`;
        const m = Math.floor((data.eta_seconds || 0) / 60);
        const s = (data.eta_seconds || 0) % 60;
        el.progEta.textContent = `ETA: ${m}:${s < 10 ? '0' : ''}${s}`;
        el.progFill.style.width = `${data.percentage}%`;
        el.progPercent.textContent = `${data.percentage}%`;
        el.btnStartGrade.disabled = true;
        el.btnStartGrade.textContent = `Grading In Progress (${data.percentage}%)...`;
      } else if (data.status === 'complete') {
        el.progFill.style.width = '100%';
        el.progPercent.textContent = '100% (Complete)';
        el.btnStartGrade.disabled = false;
        el.btnStartGrade.textContent = 'Apply LUT & Grade Selected Video';
      } else if (data.status === 'error') {
        el.progPercent.textContent = `Failed: ${data.error}`;
        el.btnStartGrade.disabled = false;
        el.btnStartGrade.textContent = 'Apply LUT & Grade Selected Video';
      }
    } catch (err) {
      // Quiet poll error
    }
  }, 1000);
}

// Bootstrap on DOM ready
document.addEventListener('DOMContentLoaded', init);
