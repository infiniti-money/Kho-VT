"use strict";
let categoryChart = null;
let trendChart = null;
let currentPayload = null;

const palette = ["#2463eb", "#31a487", "#f1a344", "#7c66d9", "#e16f7c", "#3c9fd0", "#89b64a", "#e28642", "#6783a5", "#b55aa3", "#49aaa9", "#c69135"];

function updateCharts(payload) {
  currentPayload = payload;
  const categoryCanvas = document.getElementById("categoryChart");
  const trendCanvas = document.getElementById("trendChart");
  const unitSelect = document.getElementById("trendUnit");
  const periodSelect = document.getElementById("trendPeriod");

  unitSelect.replaceChildren();
  for (const option of payload.availableUnits || []) {
    const el = document.createElement("option");
    el.value = option.value;
    el.textContent = option.label;
    unitSelect.append(el);
  }
  unitSelect.value = payload.selectedTrendUnit || "*";
  periodSelect.value = String(payload.periodDays ?? 180);
  document.getElementById("trendSubtitle").textContent = payload.trendUnit || "Số dòng giao dịch theo tháng";

  if (categoryChart) categoryChart.destroy();
  categoryChart = new Chart(categoryCanvas, {
    type: "doughnut",
    data: {
      labels: payload.categoryLabels || [],
      datasets: [{
        data: payload.categoryCounts || [],
        backgroundColor: (payload.categoryLabels || []).map((_, i) => palette[i % palette.length]),
        borderWidth: 2,
        borderColor: "#ffffff",
        hoverOffset: 8
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      cutout: "67%",
      plugins: {
        legend: { position: "bottom", labels: { color: "#667991", boxWidth: 9, boxHeight: 9, usePointStyle: true, pointStyle: "circle", padding: 11, font: { family: "Segoe UI", size: 9 } } },
        tooltip: { callbacks: { label: context => ` ${context.label}: ${context.formattedValue} mã` } }
      },
      onClick: (_event, elements) => {
        if (elements.length && window.chrome?.webview) {
          const category = categoryChart.data.labels[elements[0].index];
          window.chrome.webview.postMessage({ type: "category-filter", category });
        }
      }
    }
  });

  if (trendChart) trendChart.destroy();
  trendChart = new Chart(trendCanvas, {
    type: "line",
    data: {
      labels: payload.trendLabels || [],
      datasets: [
        { label: "Nhập", data: payload.receipts || [], borderColor: "#20a47d", backgroundColor: "rgba(32,164,125,.12)", tension: .32, fill: true, pointRadius: 2.5, pointHoverRadius: 5, borderWidth: 2 },
        { label: "Xuất", data: payload.shipments || [], borderColor: "#3579eb", backgroundColor: "rgba(53,121,235,.08)", tension: .32, fill: true, pointRadius: 2.5, pointHoverRadius: 5, borderWidth: 2 },
        { label: "Điều chuyển", data: payload.transfers || [], borderColor: "#ec9d38", backgroundColor: "rgba(236,157,56,.05)", tension: .32, fill: false, pointRadius: 2.5, pointHoverRadius: 5, borderWidth: 2, borderDash: [4, 3] },
        { label: "Điều chỉnh", data: payload.adjustments || [], borderColor: "#8b63c7", backgroundColor: "rgba(139,99,199,.05)", tension: .32, fill: false, pointRadius: 2.5, pointHoverRadius: 5, borderWidth: 1.6 }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: "index", intersect: false },
      plugins: {
        legend: { position: "top", align: "end", labels: { color: "#667991", boxWidth: 9, boxHeight: 9, usePointStyle: true, pointStyle: "circle", padding: 14, font: { family: "Segoe UI", size: 10 } } },
        tooltip: { backgroundColor: "#172b45", padding: 10, titleFont: { family: "Segoe UI", size: 11 }, bodyFont: { family: "Segoe UI", size: 10 } }
      },
      scales: {
        x: { grid: { display: false }, ticks: { color: "#8291a5", maxRotation: 0, autoSkip: true, maxTicksLimit: 9, font: { family: "Segoe UI", size: 9 } }, border: { display: false } },
        y: { beginAtZero: true, grid: { color: "#edf1f6" }, ticks: { color: "#8291a5", precision: 0, font: { family: "Segoe UI", size: 9 } }, border: { display: false } }
      }
    }
  });
}

document.getElementById("trendUnit").addEventListener("change", event => {
  if (window.chrome?.webview) window.chrome.webview.postMessage({ type: "trend-unit", value: event.target.value });
});
document.getElementById("trendPeriod").addEventListener("change", event => {
  if (window.chrome?.webview) window.chrome.webview.postMessage({ type: "trend-period", value: Number(event.target.value) });
});
window.updateCharts = updateCharts;
