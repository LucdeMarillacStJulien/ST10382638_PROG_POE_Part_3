document.addEventListener("DOMContentLoaded", function () {
    const fileInput = document.getElementById("Files");
    if (!fileInput) return;

    fileInput.addEventListener("change", function () {
        const max = parseInt(this.dataset.maxSize); // Uses data-max-size from the view
        let oversized = false;

        for (const file of this.files) {
            if (file.size > max) {
                oversized = true;
            }
        }

        if (oversized) {
            alert("One or more selected files are bigger than the maximum allowed size (10 MB).");
            this.value = ""; // Clear the selection
        }
    });
});
