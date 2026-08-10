(() => {
  const input = document.querySelector('[data-profile-photo-input]');
  const preview = document.querySelector('[data-profile-photo-preview]');
  const horizontal = document.querySelector('[data-profile-photo-horizontal]');
  const vertical = document.querySelector('[data-profile-photo-vertical]');
  const zoom = document.querySelector('[data-profile-photo-zoom]');
  const horizontalValue = document.querySelector('[data-profile-photo-horizontal-value]');
  const verticalValue = document.querySelector('[data-profile-photo-vertical-value]');
  const zoomValue = document.querySelector('[data-profile-photo-zoom-value]');

  if (!input || !preview || !horizontal || !vertical || !zoom) {
    return;
  }

  let objectUrl;

  const updatePreview = () => {
    const h = Number(horizontal.value);
    const v = Number(vertical.value);
    const z = Number(zoom.value);

    preview.style.objectPosition = `${h}% ${v}%`;
    preview.style.transformOrigin = `${h}% ${v}%`;
    preview.style.transform = `scale(${z})`;

    if (horizontalValue) {
      horizontalValue.textContent = `${h}%`;
    }
    if (verticalValue) {
      verticalValue.textContent = `${v}%`;
    }
    if (zoomValue) {
      zoomValue.textContent = `${z.toFixed(2)}x`;
    }
  };

  input.addEventListener('change', () => {
    const [file] = input.files;
    if (!file) {
      return;
    }

    if (objectUrl) {
      URL.revokeObjectURL(objectUrl);
    }

    objectUrl = URL.createObjectURL(file);
    preview.src = objectUrl;
    updatePreview();
  });

  horizontal.addEventListener('input', updatePreview);
  vertical.addEventListener('input', updatePreview);
  zoom.addEventListener('input', updatePreview);

  window.addEventListener('beforeunload', () => {
    if (objectUrl) {
      URL.revokeObjectURL(objectUrl);
    }
  });

  updatePreview();
})();
