// Dashboard charts. Data comes from the JSON block #dashboard-data rendered by Views/Dashboard/Index.cshtml.
(function () {
    const el = document.getElementById('dashboard-data');
    if (!el || typeof Chart === 'undefined') return;
    const data = JSON.parse(el.textContent);

    const css = getComputedStyle(document.documentElement);
    const color = name => css.getPropertyValue(name).trim();
    const text = color('--sebms-text'), muted = color('--sebms-muted'), grid = color('--sebms-border');
    const success = color('--sebms-success'), danger = color('--sebms-danger'), warning = color('--sebms-warning');
    const primary = color('--sebms-primary');

    const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 });
    const inrShort = v => v >= 1e7 ? '₹' + +(v / 1e7).toFixed(1) + 'Cr'
        : v >= 1e5 ? '₹' + +(v / 1e5).toFixed(1) + 'L'
        : v >= 1e3 ? '₹' + +(v / 1e3).toFixed(1) + 'k' : '₹' + v;

    Chart.defaults.font.family = "'Inter', system-ui, sans-serif";
    Chart.defaults.color = muted;

    const palette = ['#4f46e5', '#0ea5e9', '#14b8a6', '#f59e0b', '#ec4899', '#8b5cf6', '#84cc16', '#f97316', '#64748b'];

    // Spending by category: doughnut
    const spending = document.getElementById('spendingChart');
    if (spending && data.spending.length) {
        new Chart(spending, {
            type: 'doughnut',
            data: {
                labels: data.spending.map(s => s.name),
                datasets: [{
                    data: data.spending.map(s => s.total),
                    backgroundColor: data.spending.map((_, i) => palette[i % palette.length]),
                    borderColor: '#fff', borderWidth: 2
                }]
            },
            options: {
                maintainAspectRatio: false, cutout: '62%',
                plugins: {
                    legend: { position: 'right', labels: { color: text, boxWidth: 12, usePointStyle: true } },
                    tooltip: {
                        callbacks: {
                            label: ctx => ` ${ctx.label}: ${inr.format(ctx.parsed)} (${data.spending[ctx.dataIndex].pct.toFixed(1)}%)`
                        }
                    }
                }
            }
        });
    }

    // Budgets: budgeted vs spent, horizontal bars
    const budgets = document.getElementById('budgetChart');
    if (budgets && data.budgets.length) {
        // 0.8 mirrors BudgetStatus.WarningThreshold on the server.
        const spentColor = b => b.spent > b.amount ? danger : (b.amount > 0 && b.spent / b.amount >= 0.8) ? warning : success;
        new Chart(budgets, {
            type: 'bar',
            data: {
                labels: data.budgets.map(b => b.name),
                datasets: [
                    { label: 'Budget', data: data.budgets.map(b => b.amount), backgroundColor: '#c7d2fe', borderRadius: 4 },
                    { label: 'Spent', data: data.budgets.map(b => b.spent), backgroundColor: data.budgets.map(spentColor), borderRadius: 4 }
                ]
            },
            options: {
                indexAxis: 'y', maintainAspectRatio: false,
                scales: {
                    x: { beginAtZero: true, grid: { color: grid }, ticks: { callback: inrShort } },
                    y: { grid: { display: false }, ticks: { color: text } }
                },
                plugins: {
                    legend: { labels: { color: text, boxWidth: 12, usePointStyle: true } },
                    tooltip: { callbacks: { label: ctx => ` ${ctx.dataset.label}: ${inr.format(ctx.parsed.x)}` } }
                }
            }
        });
    }

    // Income vs expense by month: grouped columns
    const trend = document.getElementById('trendChart');
    if (trend) {
        new Chart(trend, {
            type: 'bar',
            data: {
                labels: data.trend.map(t => t.month),
                datasets: [
                    { label: 'Income', data: data.trend.map(t => t.income), backgroundColor: success, borderRadius: 4 },
                    { label: 'Expense', data: data.trend.map(t => t.expense), backgroundColor: danger, borderRadius: 4 }
                ]
            },
            options: {
                maintainAspectRatio: false,
                scales: {
                    x: { grid: { display: false }, ticks: { color: text } },
                    y: { beginAtZero: true, grid: { color: grid }, ticks: { callback: inrShort } }
                },
                plugins: {
                    legend: { labels: { color: text, boxWidth: 12, usePointStyle: true } },
                    tooltip: { callbacks: { label: ctx => ` ${ctx.dataset.label}: ${inr.format(ctx.parsed.y)}` } }
                }
            }
        });
    }
})();
