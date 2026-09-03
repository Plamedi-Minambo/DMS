
import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

import { getUser, logout } from "../utils/auth";
import api from "../services/api";

import "./AIInsights.css";

function AIInsights() {
    const navigate = useNavigate();

    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [errorMessage, setErrorMessage] = useState("");

    const loadInsights = useCallback(async () => {
        try {
            setLoading(true);
            setErrorMessage("");

            const response = await api.get("/AIInsights");

            setData(response.data);
        } catch (error) {
            console.error("Error loading AI insights:", error);

            if (error.response?.status === 401) {
                logout();
                navigate("/login");
                return;
            }

            if (error.response?.status === 403) {
                setErrorMessage(
                    "You do not have permission to access AI Insights."
                );
                return;
            }

            setErrorMessage(
                error.response?.data?.message ||
                "Unable to load AI insights. Please try again."
            );
        } finally {
            setLoading(false);
        }
    }, [navigate]);

    useEffect(() => {
        const currentUser = getUser();

        if (!currentUser) {
            navigate("/login");
            return;
        }

        const timer = setTimeout(() => {
            loadInsights();
        }, 0);

        return () => clearTimeout(timer);
    }, [navigate, loadInsights]);

    const handleLogout = () => {
        logout();
        navigate("/login");
    };

    const formatMoney = (value) => {
        const amount = Number(value || 0);

        return `R ${amount.toLocaleString("en-ZA", {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        })}`;
    };

    const formatNumber = (value) => {
        return Number(value || 0).toLocaleString("en-ZA");
    };

    const formatPercentage = (value) => {
        return `${Number(value || 0).toFixed(2)}%`;
    };

    const formatDate = (value) => {
        if (!value) {
            return "N/A";
        }

        const date = new Date(value);

        if (Number.isNaN(date.getTime())) {
            return "N/A";
        }

        return date.toLocaleDateString("en-ZA", {
            day: "2-digit",
            month: "short",
            year: "numeric"
        });
    };

    const formatPeriod = (period) => {
        if (!period) {
            return "N/A";
        }

        const parts = period.split("-");

        if (parts.length !== 2) {
            return period;
        }

        const year = Number(parts[0]);
        const month = Number(parts[1]);

        const date = new Date(year, month - 1, 1);

        return date.toLocaleDateString("en-ZA", {
            month: "short",
            year: "numeric"
        });
    };

    const getMaxSpend = () => {
        if (!data?.spendingTrend?.length) {
            return 0;
        }

        return Math.max(
            ...data.spendingTrend.map(
                (item) => Number(item.totalSpend || 0)
            )
        );
    };

    const getBarHeight = (value) => {
        const maxSpend = getMaxSpend();

        if (maxSpend === 0) {
            return 5;
        }

        return Math.max(
            8,
            (Number(value || 0) / maxSpend) * 100
        );
    };

    if (loading) {
        return (
            <div className="ai-page">

                <aside className="ai-sidebar">

                    <div className="ai-sidebar-brand">
                        <div className="ai-brand-icon">📁</div>

                        <div>
                            <h2>Document Management</h2>
                            <span>System</span>
                        </div>
                    </div>

                    <nav className="ai-sidebar-nav">

                        <button
                            className="ai-nav-item"
                            onClick={() => navigate("/dashboard")}
                        >
                            <span>🏠</span>
                            Dashboard
                        </button>

                        <button
                            className="ai-nav-item"
                            onClick={() => navigate("/documents")}
                        >
                            <span>📄</span>
                            Documents
                        </button>

                        <button
                            className="ai-nav-item"
                            onClick={() => navigate("/reports")}
                        >
                            <span>📊</span>
                            Reports
                        </button>

                        <button
                            className="ai-nav-item active"
                            onClick={() => navigate("/ai-insights")}
                        >
                            <span>🤖</span>
                            AI Insights
                        </button>

                        <button
                            className="ai-nav-item"
                            onClick={() =>
                                navigate("/approval-workflow")
                            }
                        >
                            <span>🔐</span>
                            Approval Workflow
                        </button>

                    </nav>

                    <button
                        className="ai-logout-button"
                        onClick={handleLogout}
                    >
                        <span>🚪</span>
                        Logout
                    </button>

                </aside>

                <main className="ai-main">

                    <div className="ai-loading">

                        <div className="ai-spinner"></div>

                        <h2>Analysing documents...</h2>

                        <p>
                            Generating financial and document insights.
                        </p>

                    </div>

                </main>

            </div>
        );
    }

    if (errorMessage) {
        return (
            <div className="ai-page">

                <aside className="ai-sidebar">

                    <div className="ai-sidebar-brand">
                        <div className="ai-brand-icon">📁</div>

                        <div>
                            <h2>Document Management</h2>
                            <span>System</span>
                        </div>
                    </div>

                    <nav className="ai-sidebar-nav">

                        <button
                            className="ai-nav-item"
                            onClick={() => navigate("/dashboard")}
                        >
                            <span>🏠</span>
                            Dashboard
                        </button>

                        <button
                            className="ai-nav-item"
                            onClick={() => navigate("/documents")}
                        >
                            <span>📄</span>
                            Documents
                        </button>

                        <button
                            className="ai-nav-item"
                            onClick={() => navigate("/reports")}
                        >
                            <span>📊</span>
                            Reports
                        </button>

                        <button
                            className="ai-nav-item active"
                            onClick={() => navigate("/ai-insights")}
                        >
                            <span>🤖</span>
                            AI Insights
                        </button>

                        <button
                            className="ai-nav-item"
                            onClick={() =>
                                navigate("/approval-workflow")
                            }
                        >
                            <span>🔐</span>
                            Approval Workflow
                        </button>

                    </nav>

                    <button
                        className="ai-logout-button"
                        onClick={handleLogout}
                    >
                        <span>🚪</span>
                        Logout
                    </button>

                </aside>

                <main className="ai-main">

                    <div className="ai-message">

                        <div className="ai-message-icon">
                            ⚠️
                        </div>

                        <h2>Unable to Load AI Insights</h2>

                        <p>{errorMessage}</p>

                        <button
                            className="ai-primary-button"
                            onClick={loadInsights}
                        >
                            Try Again
                        </button>

                    </div>

                </main>

            </div>
        );
    }

    if (!data?.hasData) {
        return (
            <div className="ai-page">

                <aside className="ai-sidebar">

                    <div className="ai-sidebar-brand">
                        <div className="ai-brand-icon">📁</div>

                        <div>
                            <h2>Document Management</h2>
                            <span>System</span>
                        </div>
                    </div>

                    <nav className="ai-sidebar-nav">

                        <button
                            className="ai-nav-item"
                            onClick={() => navigate("/dashboard")}
                        >
                            <span>🏠</span>
                            Dashboard
                        </button>

                        <button
                            className="ai-nav-item"
                            onClick={() => navigate("/documents")}
                        >
                            <span>📄</span>
                            Documents
                        </button>

                        <button
                            className="ai-nav-item"
                            onClick={() => navigate("/reports")}
                        >
                            <span>📊</span>
                            Reports
                        </button>

                        <button
                            className="ai-nav-item active"
                            onClick={() => navigate("/ai-insights")}
                        >
                            <span>🤖</span>
                            AI Insights
                        </button>

                        <button
                            className="ai-nav-item"
                            onClick={() =>
                                navigate("/approval-workflow")
                            }
                        >
                            <span>🔐</span>
                            Approval Workflow
                        </button>

                    </nav>

                    <button
                        className="ai-logout-button"
                        onClick={handleLogout}
                    >
                        <span>🚪</span>
                        Logout
                    </button>

                </aside>

                <main className="ai-main">

                    <div className="ai-message">

                        <div className="ai-message-icon">
                            🤖
                        </div>

                        <h2>No Invoice Data Available</h2>

                        <p>
                            Upload invoice or credit note documents
                            to generate AI insights.
                        </p>

                        <button
                            className="ai-primary-button"
                            onClick={() => navigate("/documents")}
                        >
                            Go to Documents
                        </button>

                    </div>

                </main>

            </div>
        );
    }

    const summary = data.summary || {};
    const topVendor = data.topVendor;
    const spendingTrend = data.spendingTrend || [];
    const vendorAnalysis = data.vendorAnalysis || [];
    const insights = data.insights || [];
    const anomalyDetection = data.anomalyDetection || {};
    const anomalies = anomalyDetection.anomalies || [];

    return (
        <div className="ai-page">

            <aside className="ai-sidebar">

                <div className="ai-sidebar-brand">

                    <div className="ai-brand-icon">
                        📁
                    </div>

                    <div>
                        <h2>Document Management</h2>
                        <span>System</span>
                    </div>

                </div>

                <nav className="ai-sidebar-nav">

                    <button
                        className="ai-nav-item"
                        onClick={() => navigate("/dashboard")}
                    >
                        <span>🏠</span>
                        Dashboard
                    </button>

                    <button
                        className="ai-nav-item"
                        onClick={() => navigate("/documents")}
                    >
                        <span>📄</span>
                        Documents
                    </button>

                    <button
                        className="ai-nav-item"
                        onClick={() => navigate("/reports")}
                    >
                        <span>📊</span>
                        Reports
                    </button>

                    <button
                        className="ai-nav-item active"
                        onClick={() => navigate("/ai-insights")}
                    >
                        <span>🤖</span>
                        AI Insights
                    </button>

                    <button
                        className="ai-nav-item"
                        onClick={() =>
                            navigate("/approval-workflow")
                        }
                    >
                        <span>🔐</span>
                        Approval Workflow
                    </button>

                </nav>

                <div className="ai-sidebar-user">

                    <div className="ai-avatar">
                        {(getUser()?.email || "U")
                            .charAt(0)
                            .toUpperCase()}
                    </div>

                    <div className="ai-user-details">

                        <strong>
                            {getUser()?.email || "User"}
                        </strong>

                        <span>
                            {getUser()?.role || "User"}
                        </span>

                    </div>

                </div>

                <button
                    className="ai-logout-button"
                    onClick={handleLogout}
                >
                    <span>🚪</span>
                    Logout
                </button>

            </aside>

            <main className="ai-main">

                <header className="ai-header">

                    <div className="ai-heading">

                        <div className="ai-heading-icon">
                            🤖
                        </div>

                        <div>

                            <h1>AI Insights</h1>

                            <p>
                                Intelligent analysis of your
                                invoice and financial data.
                            </p>

                        </div>

                    </div>

                    <button
                        className="ai-refresh-button"
                        onClick={loadInsights}
                    >
                        ↻ Refresh Analysis
                    </button>

                </header>

                <div className="ai-status">

                    <div className="ai-status-icon">
                        ✨
                    </div>

                    <div>

                        <strong>
                            AI Analysis Active
                        </strong>

                        <p>
                            Analysis is based on your uploaded
                            invoice and credit note data.
                        </p>

                    </div>

                </div>

                <section className="ai-section">

                    <div className="ai-section-title">

                        <h2>Financial Overview</h2>

                        <p>
                            Key financial indicators from
                            analysed documents.
                        </p>

                    </div>

                    <div className="ai-summary-grid">

                        <div className="ai-card">

                            <div className="ai-card-icon">
                                💰
                            </div>

                            <div>

                                <span>Total Spend</span>

                                <strong>
                                    {formatMoney(
                                        summary.totalIncludingVAT
                                    )}
                                </strong>

                                <small>
                                    Including VAT
                                </small>

                            </div>

                        </div>

                        <div className="ai-card">

                            <div className="ai-card-icon">
                                📄
                            </div>

                            <div>

                                <span>Documents Analysed</span>

                                <strong>
                                    {formatNumber(
                                        summary.totalDocuments
                                    )}
                                </strong>

                                <small>
                                    Invoice documents
                                </small>

                            </div>

                        </div>

                        <div className="ai-card">

                            <div className="ai-card-icon">
                                📈
                            </div>

                            <div>

                                <span>Average Invoice</span>

                                <strong>
                                    {formatMoney(
                                        summary.averageInvoice
                                    )}
                                </strong>

                                <small>
                                    Average document value
                                </small>

                            </div>

                        </div>

                        <div className="ai-card">

                            <div className="ai-card-icon">
                                🧾
                            </div>

                            <div>

                                <span>Total VAT</span>

                                <strong>
                                    {formatMoney(
                                        summary.totalVAT
                                    )}
                                </strong>

                                <small>
                                    {formatPercentage(
                                        summary.vatPercentage
                                    )} of pre-VAT spend
                                </small>

                            </div>

                        </div>

                    </div>

                </section>

                <section className="ai-metrics">

                    <div className="ai-metric">

                        <span>Pending Approval</span>

                        <strong>
                            {formatNumber(
                                summary.pendingDocuments
                            )}
                        </strong>

                        <small>
                            Awaiting approval
                        </small>

                    </div>

                    <div className="ai-metric">

                        <span>Approved</span>

                        <strong>
                            {formatNumber(
                                summary.approvedDocuments
                            )}
                        </strong>

                        <small>
                            Approved documents
                        </small>

                    </div>

                    <div className="ai-metric">

                        <span>Rejected</span>

                        <strong>
                            {formatNumber(
                                summary.rejectedDocuments
                            )}
                        </strong>

                        <small>
                            Requires attention
                        </small>

                    </div>

                    <div className="ai-metric">

                        <span>Detected Anomalies</span>

                        <strong>
                            {formatNumber(
                                anomalyDetection.anomalyCount
                            )}
                        </strong>

                        <small>
                            Unusually high invoices
                        </small>

                    </div>

                </section>

                <section className="ai-columns">

                    <div className="ai-panel">

                        <div className="ai-panel-header">

                            <div>

                                <h2>
                                    Highest-Spending Vendor
                                </h2>

                                <p>
                                    Vendor with the highest
                                    total invoice value.
                                </p>

                            </div>

                            <span>🏆</span>

                        </div>

                        {topVendor ? (

                            <div className="top-vendor">

                                <div className="vendor-info">

                                    <div className="vendor-avatar">
                                        {topVendor.vendor
                                            ?.charAt(0)
                                            .toUpperCase() || "V"}
                                    </div>

                                    <div>

                                        <h3>
                                            {topVendor.vendor}
                                        </h3>

                                        <p>
                                            {formatNumber(
                                                topVendor.invoiceCount
                                            )} invoices
                                        </p>

                                    </div>

                                </div>

                                <div className="vendor-total">

                                    <span>
                                        Total Spend
                                    </span>

                                    <strong>
                                        {formatMoney(
                                            topVendor.totalSpend
                                        )}
                                    </strong>

                                </div>

                                <div className="vendor-total">

                                    <span>
                                        Average Invoice
                                    </span>

                                    <strong>
                                        {formatMoney(
                                            topVendor.averageSpend
                                        )}
                                    </strong>

                                </div>

                            </div>

                        ) : (

                            <div className="empty">
                                No vendor data available.
                            </div>

                        )}

                    </div>

                    <div className="ai-panel">

                        <div className="ai-panel-header">

                            <div>

                                <h2>
                                    AI Recommendations
                                </h2>

                                <p>
                                    Automated observations from
                                    your invoice data.
                                </p>

                            </div>

                            <span>💡</span>

                        </div>

                        <div className="insights">

                            {insights.length > 0 ? (

                                insights.map((insight, index) => (

                                    <div
                                        className="insight"
                                        key={index}
                                    >

                                        <div className="insight-number">
                                            {index + 1}
                                        </div>

                                        <p>
                                            {insight}
                                        </p>

                                    </div>

                                ))

                            ) : (

                                <div className="empty">
                                    No insights available.
                                </div>

                            )}

                        </div>

                    </div>

                </section>

                <section className="ai-panel">

                    <div className="ai-panel-header">

                        <div>

                            <h2>
                                Spending Trend
                            </h2>

                            <p>
                                Monthly spending based on
                                uploaded documents.
                            </p>

                        </div>

                        <span>📊</span>

                    </div>

                    {spendingTrend.length > 0 ? (

                        <div className="trend">

                            <div className="trend-values">

                                {spendingTrend.map((item) => (

                                    <div
                                        className="trend-item"
                                        key={item.period}
                                    >

                                        <div className="trend-bar-container">

                                            <div
                                                className="trend-bar"
                                                style={{
                                                    height: `${getBarHeight(
                                                        item.totalSpend
                                                    )}%`
                                                }}
                                            ></div>

                                        </div>

                                        <strong>
                                            {formatMoney(
                                                item.totalSpend
                                            )}
                                        </strong>

                                        <span>
                                            {formatPeriod(
                                                item.period
                                            )}
                                        </span>

                                        <small>
                                            {formatNumber(
                                                item.invoiceCount
                                            )} invoices
                                        </small>

                                    </div>

                                ))}

                            </div>

                        </div>

                    ) : (

                        <div className="empty">
                            No spending trend data available.
                        </div>

                    )}

                </section>

                <section className="ai-panel">

                    <div className="ai-panel-header">

                        <div>

                            <h2>
                                Top Vendors
                            </h2>

                            <p>
                                Vendors ranked by total invoice value.
                            </p>

                        </div>

                        <span>🏢</span>

                    </div>

                    {vendorAnalysis.length > 0 ? (

                        <div className="table-container">

                            <table>

                                <thead>

                                    <tr>
                                        <th>#</th>
                                        <th>Vendor</th>
                                        <th>Invoices</th>
                                        <th>Average</th>
                                        <th>Total Spend</th>
                                    </tr>

                                </thead>

                                <tbody>

                                    {vendorAnalysis.map(
                                        (vendor, index) => (

                                            <tr key={vendor.vendor}>

                                                <td>
                                                    <span className="rank">
                                                        {index + 1}
                                                    </span>
                                                </td>

                                                <td>
                                                    <strong>
                                                        {vendor.vendor}
                                                    </strong>
                                                </td>

                                                <td>
                                                    {formatNumber(
                                                        vendor.invoiceCount
                                                    )}
                                                </td>

                                                <td>
                                                    {formatMoney(
                                                        vendor.averageSpend
                                                    )}
                                                </td>

                                                <td>
                                                    <strong>
                                                        {formatMoney(
                                                            vendor.totalSpend
                                                        )}
                                                    </strong>
                                                </td>

                                            </tr>

                                        )
                                    )}

                                </tbody>

                            </table>

                        </div>

                    ) : (

                        <div className="empty">
                            No vendor analysis available.
                        </div>

                    )}

                </section>

                <section className="ai-panel">

                    <div className="ai-panel-header">

                        <div>

                            <h2>
                                Anomaly Detection
                            </h2>

                            <p>
                                Identifies invoice values that are
                                significantly above the normal range.
                            </p>

                        </div>

                        <span>🚨</span>

                    </div>

                    <div className="anomaly-summary">

                        <div>

                            <span>
                                Average Invoice
                            </span>

                            <strong>
                                {formatMoney(
                                    anomalyDetection.averageInvoiceValue
                                )}
                            </strong>

                        </div>

                        <div>

                            <span>
                                Standard Deviation
                            </span>

                            <strong>
                                {formatMoney(
                                    anomalyDetection.standardDeviation
                                )}
                            </strong>

                        </div>

                        <div>

                            <span>
                                Anomaly Threshold
                            </span>

                            <strong>
                                {formatMoney(
                                    anomalyDetection.anomalyThreshold
                                )}
                            </strong>

                        </div>

                        <div>

                            <span>
                                Detected
                            </span>

                            <strong
                                className={
                                    anomalies.length > 0
                                        ? "danger"
                                        : "success"
                                }
                            >
                                {formatNumber(
                                    anomalies.length
                                )}
                            </strong>

                        </div>

                    </div>

                    {anomalies.length > 0 ? (

                        <div className="anomaly-list">

                            {anomalies.map((anomaly) => (

                                <div
                                    className="anomaly"
                                    key={anomaly.documentId}
                                >

                                    <div className="anomaly-icon">
                                        ⚠️
                                    </div>

                                    <div className="anomaly-content">

                                        <div className="anomaly-title">

                                            <strong>
                                                {anomaly.invoiceNumber ||
                                                    anomaly.fileName}
                                            </strong>

                                            <span>
                                                {formatMoney(
                                                    anomaly.amount
                                                )}
                                            </span>

                                        </div>

                                        <p>
                                            Vendor:{" "}
                                            {anomaly.vendor ||
                                                "Unknown Vendor"}
                                        </p>

                                        <small>
                                            Invoice date:{" "}
                                            {formatDate(
                                                anomaly.invoiceDate
                                            )}
                                        </small>

                                        <div className="anomaly-reason">
                                            {anomaly.reason}
                                        </div>

                                    </div>

                                </div>

                            ))}

                        </div>

                    ) : (

                        <div className="no-anomalies">

                            <div className="check">
                                ✓
                            </div>

                            <div>

                                <strong>
                                    No unusual invoices detected
                                </strong>

                                <p>
                                    No invoice values exceeded the
                                    current statistical threshold.
                                </p>

                            </div>

                        </div>

                    )}

                </section>

                <footer className="ai-footer">
                    AI Insights are generated using statistical
                    analysis and automated business rules based
                    on available invoice data.
                </footer>

            </main>

        </div>
    );
}

export default AIInsights;

