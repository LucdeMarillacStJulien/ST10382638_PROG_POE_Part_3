// wwwroot/js/claim-automation.js
// Live auto-calculation for Lecturer claim form.
// Uses the HoursWorked input and the readonly rateAtSubmission input
// to preview CalculatedAmount on the page (and keeps a hidden field in sync).

document.addEventListener('DOMContentLoaded', function () {
    var hoursInput = document.getElementById('HoursWorked');
    var rateInput = document.querySelector('input[name="rateAtSubmission"]');
    var totalPreview = document.getElementById('CalculatedAmountPreview');
    var hiddenTotal = document.getElementById('CalculatedAmount');

    if (!hoursInput || !rateInput || !totalPreview || !hiddenTotal) {
        // If any of these are missing, do nothing on other pages.
        return;
    }

    function toNumber(value) {
        if (value === null || value === undefined) return NaN;
        var normalized = value.toString().trim().replace(',', '.');
        if (!normalized) return NaN;
        return parseFloat(normalized);
    }

    function updateTotal() {
        var hours = toNumber(hoursInput.value);
        var rate = toNumber(rateInput.value);

        if (isNaN(hours) || isNaN(rate) || hours <= 0) {
            totalPreview.textContent = '0.00';
            hiddenTotal.value = '0';
            return;
        }

        var total = hours * rate;
        totalPreview.textContent = total.toFixed(2);
        hiddenTotal.value = total.toFixed(2);
    }

    hoursInput.addEventListener('input', updateTotal);
    hoursInput.addEventListener('change', updateTotal);

    // Initial calculation (in case there is a pre-filled value)
    updateTotal();
});
