(() => {
  const input = document.querySelector('[data-profile-photo-input]');
  const preview = document.querySelector('[data-profile-photo-preview]');
  const position = document.querySelector('[data-profile-photo-position]');

  if (!input || !preview || !position) {
    return;
  }

  let objectUrl;

  const updatePreviewPosition = () => {
    preview.style.objectPosition = `50% ${position.value}%`;
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
    updatePreviewPosition();
  });

  position.addEventListener('input', updatePreviewPosition);
  window.addEventListener('beforeunload', () => {
    if (objectUrl) {
      URL.revokeObjectURL(objectUrl);
    }
  });

  updatePreviewPosition();
})();
