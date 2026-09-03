
import {
    useCallback,
    useEffect,
    useMemo,
    useState
} from "react";

import { useNavigate } from "react-router-dom";

import { getUser, logout } from "../utils/auth";
import api from "../services/api";

import "./Reports.css";

function Reports() {
    const navigate = useNavigate();
    const user = getUser();

    const [report, setReport] = useState(null);

    const [loading, setLoading] = useState(false);
    const [exporting, setExporting] = useState(false);
    const [exportingPdf, setExportingPdf] = useState(false);

    const [reportType, setReportType] =
        useState("spend");

    const [startDate, setStartDate] =
        useState("");

    const [endDate, setEndDate] =
        useState("");

    const [vendor, setVendor] =
        useState("");

    const [status, setStatus] =
        useState("");

    const [minAmount, setMinAmount] =
        useState("");

    const [maxAmount, setMaxAmount] =
        useState("");

    const [errorMessage, setErrorMessage] =
        useState("");

    const [validationMessage, setValidationMessage] =
        useState("");

    const [authorizationMessage, setAuthorizationMessage] =
        useState("");

    const showAuthorizationPopup = (message) => {
        setAuthorizationMessage(message);
    };

    const closeAuthorizationPopup = () => {
        setAuthorizationMessage("");
    };

    // ========================================
    // BUILD REPORT PARAMETERS
    // ========================================

    const buildReportParams = useCallback(() => {
        const params = {};

        params.reportType = reportType;

        if (startDate) {
            params.startDate = startDate;
        }

        if (endDate) {
            params.endDate = endDate;
        }

        if (vendor.trim()) {
            params.vendor = vendor.trim();
        }

        if (status) {
            params.status = status;
        }

        if (minAmount !== "") {
            params.minAmount = minAmount;
        }

        if (maxAmount !== "") {
            params.maxAmount = maxAmount;
        }

        return params;
    }, [
        reportType,
        startDate,
        endDate,
        vendor,
        status,
        minAmount,
        maxAmount
    ]);

    // ========================================
    // VALIDATE FILTERS
    // ========================================

    const validateFilters = () => {
        if (
            startDate &&
            endDate &&
            startDate > endDate
        ) {
            return "Start date cannot be later than end date.";
        }

        if (
            minAmount !== "" &&
            Number(minAmount) < 0
        ) {
            return "Minimum amount cannot be negative.";
        }

        if (
            maxAmount !== "" &&
            Number(maxAmount) < 0
        ) {
            return "Maximum amount cannot be negative.";
        }

        if (
            minAmount !== "" &&
            maxAmount !== "" &&
            Number(minAmount) > Number(maxAmount)
        ) {
            return "Minimum amount cannot be greater than maximum amount.";
        }

        return "";
    };

    // ========================================
    // LOAD REPORT
    // ========================================

    const loadReport = useCallback(async () => {
        try {
            setLoading(true);
            setErrorMessage("");
            setValidationMessage("");

            const validation =
                validateFilters();

            if (validation) {
                setValidationMessage(validation);
                setLoading(false);
                return;
            }

            const params =
                buildReportParams();

            const response =
                await api.get(
                    "/Reports",
                    {
                        params
                    }
                );

            setReport(response.data);

        } catch (error) {
            console.error(
                "Report loading error:",
                error
            );

            if (
                error.response?.status === 401
            ) {
                logout();
                navigate("/login");
                return;
            }

            if (
                error.response?.status === 403
            ) {
                showAuthorizationPopup(
                    "You do not have authorization to view reports."
                );
                return;
            }

            setErrorMessage(
                error.response?.data ||
                "Failed to load the report."
            );

        } finally {
            setLoading(false);
        }
    }, [
        buildReportParams,
        navigate,
        startDate,
        endDate,
        minAmount,
        maxAmount
    ]);

    // ========================================
    // INITIAL REPORT LOAD
    // ========================================

    useEffect(() => {
        const timer = setTimeout(() => {
            loadReport();
        }, 0);

        return () => {
            clearTimeout(timer);
        };
    }, [loadReport]);

    // ========================================
    // RESET FILTERS
    // ========================================

    const handleResetFilters = () => {
        setStartDate("");
        setEndDate("");
        setVendor("");
        setStatus("");
        setMinAmount("");
        setMaxAmount("");
        setValidationMessage("");
        setErrorMessage("");

        setTimeout(() => {
            loadReport();
        }, 0);
    };

    // ========================================
    // EXPORT EXCEL
    // ========================================

    const handleExportExcel = async () => {
        try {
            setExporting(true);
            setErrorMessage("");
            setValidationMessage("");

            const validation =
                validateFilters();

            if (validation) {
                setValidationMessage(validation);
                setExporting(false);
                return;
            }

            const params =
                buildReportParams();

            const response =
                await api.get(
                    "/Reports/export/excel",
                    {
                        params,
                        responseType: "blob"
                    }
                );

            const blob =
                new Blob(
                    [response.data],
                    {
                        type:
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    }
                );

            const url =
                window.URL.createObjectURL(
                    blob
                );

            const link =
                document.createElement("a");

            link.href = url;

            link.download =
                "Document_Report.xlsx";

            document.body.appendChild(link);

            link.click();

            link.remove();

            window.URL.revokeObjectURL(url);

        } catch (error) {
            console.error(
                "Excel export error:",
                error
            );

            if (
                error.response?.status === 401
            ) {
                logout();
                navigate("/login");
                return;
            }

            if (
                error.response?.status === 403
            ) {
                showAuthorizationPopup(
                    "You do not have authorization to export reports."
                );
                return;
            }

            setErrorMessage(
                "Failed to export the Excel report."
            );

        } finally {
            setExporting(false);
        }
    };

    // ========================================
    // EXPORT PDF
    // ========================================

    const handleExportPdf = async () => {
        try {
            setExportingPdf(true);
            setErrorMessage("");
            setValidationMessage("");

            const validation =
                validateFilters();

            if (validation) {
                setValidationMessage(validation);
                setExportingPdf(false);
                return;
            }

            const params =
                buildReportParams();

            const response =
                await api.get(
                    "/Reports/export/pdf",
                    {
                        params,
                        responseType: "blob"
                    }
                );

            const blob =
                new Blob(
                    [response.data],
                    {
                        type: "application/pdf"
                    }
                );

            const url =
                window.URL.createObjectURL(
                    blob
                );

            const link =
                document.createElement("a");

            link.href = url;

            link.download =
                "Document_Report.pdf";

            document.body.appendChild(link);

            link.click();

            link.remove();

            window.URL.revokeObjectURL(url);

        } catch (error) {
            console.error(
                "PDF export error:",
                error
            );

            if (
                error.response?.status === 401
            ) {
                logout();
                navigate("/login");
                return;
            }

            if (
                error.response?.status === 403
            ) {
                showAuthorizationPopup(
                    "You do not have authorization to export reports."
                );
                return;
            }

            setErrorMessage(
                "Failed to export the PDF report."
            );

        } finally {
            setExportingPdf(false);
        }
    };

    // ========================================
    // FORMAT MONEY
    // ========================================

    const formatMoney = (amount) => {
        if (
            amount === null ||
            amount === undefined
        ) {
            return "R 0.00";
        }

        return `R ${Number(
            amount
        ).toLocaleString(
            "en-ZA",
            {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            }
        )}`;
    };

    // ========================================
    // FORMAT DATE
    // ========================================

    const formatDate = (date) => {
        if (!date) {
            return "-";
        }

        return new Date(
            date
        ).toLocaleDateString(
            "en-ZA",
            {
                year: "numeric",
                month: "short",
                day: "numeric"
            }
        );
    };

    // ========================================
    // VENDOR ANALYSIS
    // ========================================

    const vendorAnalysis = useMemo(() => {
        if (
            !report?.documents ||
            report.documents.length === 0
        ) {
            return [];
        }

        const vendors = {};

        report.documents.forEach(
            (document) => {
                const vendorName =
                    document.vendor?.trim() ||
                    "Unknown Vendor";

                if (!vendors[vendorName]) {
                    vendors[vendorName] = {
                        vendor:
                            vendorName,
                        invoices: 0,
                        amount: 0,
                        vat: 0,
                        total: 0
                    };
                }

                vendors[vendorName].invoices += 1;

                vendors[vendorName].amount +=
                    Number(
                        document.amount || 0
                    );

                vendors[vendorName].vat +=
                    Number(
                        document.vat || 0
                    );

                vendors[vendorName].total +=
                    Number(
                        document.totalAmount || 0
                    );
            }
        );

        return Object.values(vendors)
            .sort(
                (a, b) =>
                    b.total - a.total
            );

    }, [report]);

    // ========================================
    // ANALYTICS
    // ========================================

    const analytics = useMemo(() => {
        const documents =
            report?.documents || [];

        const pending =
            documents.filter(
                (document) =>
                    document.status === "Pending" ||
                    document.status === "Pending Manager" ||
                    document.status === "Pending Finance"
            ).length;

        const approved =
            documents.filter(
                (document) =>
                    document.status === "Approved"
            ).length;

        const rejected =
            documents.filter(
                (document) =>
                    document.status === "Rejected"
            ).length;

        const maximumVendorTotal =
            vendorAnalysis.length > 0
                ? Math.max(
                    ...vendorAnalysis.map(
                        (item) =>
                            item.total
                    )
                )
                : 0;

        return {
            pending,
            approved,
            rejected,
            maximumVendorTotal
        };

    }, [
        report,
        vendorAnalysis
    ]);

    // ========================================
    // LOGOUT
    // ========================================

    const handleLogout = () => {
        logout();
        navigate("/login");
    };

    return (
        <div className="reports-page">

            {/* ========================================
                AUTHORIZATION POPUP
            ======================================== */}

            {authorizationMessage && (
                <div className="authorization-overlay">
                    <div className="authorization-popup">

                        <div className="authorization-icon">
                            🔒
                        </div>

                        <h2>
                            Access Denied
                        </h2>

                        <p>
                            {authorizationMessage}
                        </p>

                        <button
                            type="button"
                            className="authorization-button"
                            onClick={
                                closeAuthorizationPopup
                            }
                        >
                            OK
                        </button>

                    </div>
                </div>
            )}

            {/* ========================================
                SIDEBAR
            ======================================== */}

            <aside className="sidebar">

                <div className="sidebar-logo">

                    <div className="logo-icon">
                        📄
                    </div>

                    <div>
                        <h2>
                            Document
                        </h2>

                        <span>
                            Management
                        </span>
                    </div>

                </div>

                <nav className="sidebar-nav">

                    <button
                        type="button"
                        className="nav-item"
                        onClick={() =>
                            navigate("/dashboard")
                        }
                    >
                        <span>📊</span>
                        Dashboard
                    </button>

                    <button
                        type="button"
                        className="nav-item"
                        onClick={() =>
                            navigate("/documents")
                        }
                    >
                        <span>📁</span>
                        Documents
                    </button>

                    <button
                        type="button"
                        className="nav-item active"
                    >
                        <span>📊</span>
                        Reports
                    </button>

                    {/* AI INSIGHTS */}
                    <button
                        type="button"
                        className="nav-item"
                        onClick={() =>
                            navigate("/ai-insights")
                        }
                    >
                        <span>🤖</span>
                        AI Insights
                    </button>

                    <button
                        type="button"
                        className="nav-item"
                        onClick={() =>
                            navigate("/approval-workflow")
                        }
                    >
                        <span>🔐</span>
                        Approval Workflow
                    </button>

                </nav>

                <div className="sidebar-bottom">

                    <button
                        type="button"
                        className="nav-item logout-item"
                        onClick={handleLogout}
                    >
                        <span>🚪</span>
                        Logout
                    </button>

                </div>

            </aside>

            {/* ========================================
                MAIN CONTENT
            ======================================== */}

            <main className="main-content">

                {/* ========================================
                    HEADER
                ======================================== */}

                <header className="page-header">

                    <div>
                        <h1>
                            Reports & Analytics
                        </h1>

                        <p>
                            Analyse spending, vendors, approvals
                            and VAT information
                        </p>
                    </div>

                    <div className="user-info">

                        <div className="user-avatar">
                            {user?.fullName
                                ?.charAt(0)
                                ?.toUpperCase() || "U"}
                        </div>

                        <div className="user-details">

                            <strong>
                                {user?.fullName || "User"}
                            </strong>

                            <span>
                                {user?.role || "User"}
                            </span>

                        </div>

                    </div>

                </header>

                {/* ========================================
                    REPORT TYPE
                ======================================== */}

                <section className="reports-type-section">

                    <div className="section-heading">

                        <div>
                            <h2>
                                Report Type
                            </h2>

                            <p>
                                Select the type of report you want
                                to generate
                            </p>
                        </div>

                    </div>

                    <div className="report-type-grid">

                        <button
                            type="button"
                            className={`report-type-card ${
                                reportType === "spend"
                                    ? "selected"
                                    : ""
                            }`}
                            onClick={() =>
                                setReportType("spend")
                            }
                        >
                            <span>
                                💰
                            </span>

                            <strong>
                                Spend Summary
                            </strong>

                            <small>
                                View overall spending and approval
                                totals
                            </small>
                        </button>

                        <button
                            type="button"
                            className={`report-type-card ${
                                reportType === "vendor"
                                    ? "selected"
                                    : ""
                            }`}
                            onClick={() =>
                                setReportType("vendor")
                            }
                        >
                            <span>
                                🏢
                            </span>

                            <strong>
                                Vendor Analysis
                            </strong>

                            <small>
                                Analyse spending by vendor
                            </small>
                        </button>

                        <button
                            type="button"
                            className={`report-type-card ${
                                reportType === "tax"
                                    ? "selected"
                                    : ""
                            }`}
                            onClick={() =>
                                setReportType("tax")
                            }
                        >
                            <span>
                                🧾
                            </span>

                            <strong>
                                Tax / VAT Report
                            </strong>

                            <small>
                                Review VAT and invoice amounts
                            </small>
                        </button>

                    </div>

                </section>

                {/* ========================================
                    FILTERS
                ======================================== */}

                <section className="reports-filter-section">

                    <div className="section-heading">

                        <div>
                            <h2>
                                Report Filters
                            </h2>

                            <p>
                                Refine your report results
                            </p>
                        </div>

                    </div>

                    <div className="reports-filter-grid">

                        <div className="report-form-group">

                            <label>
                                Start Date
                            </label>

                            <input
                                type="date"
                                value={startDate}
                                onChange={(event) =>
                                    setStartDate(
                                        event.target.value
                                    )
                                }
                            />

                        </div>

                        <div className="report-form-group">

                            <label>
                                End Date
                            </label>

                            <input
                                type="date"
                                value={endDate}
                                onChange={(event) =>
                                    setEndDate(
                                        event.target.value
                                    )
                                }
                            />

                        </div>

                        <div className="report-form-group">

                            <label>
                                Vendor Name
                            </label>

                            <input
                                type="text"
                                placeholder="Search vendor..."
                                value={vendor}
                                onChange={(event) =>
                                    setVendor(
                                        event.target.value
                                    )
                                }
                            />

                        </div>

                        <div className="report-form-group">

                            <label>
                                Approval Status
                            </label>

                            <select
                                value={status}
                                onChange={(event) =>
                                    setStatus(
                                        event.target.value
                                    )
                                }
                            >
                                <option value="">
                                    All Statuses
                                </option>

                                <option value="Pending">
                                    Pending
                                </option>

                                <option value="Approved">
                                    Approved
                                </option>

                                <option value="Rejected">
                                    Rejected
                                </option>
                            </select>

                        </div>

                        <div className="report-form-group">

                            <label>
                                Minimum Amount
                            </label>

                            <input
                                type="number"
                                min="0"
                                step="0.01"
                                placeholder="R 0.00"
                                value={minAmount}
                                onChange={(event) =>
                                    setMinAmount(
                                        event.target.value
                                    )
                                }
                            />

                        </div>

                        <div className="report-form-group">

                            <label>
                                Maximum Amount
                            </label>

                            <input
                                type="number"
                                min="0"
                                step="0.01"
                                placeholder="R 0.00"
                                value={maxAmount}
                                onChange={(event) =>
                                    setMaxAmount(
                                        event.target.value
                                    )
                                }
                            />

                        </div>

                    </div>

                    <div className="report-filter-actions">

                        <button
                            type="button"
                            className="generate-report-button"
                            onClick={loadReport}
                            disabled={loading}
                        >
                            🔍{" "}
                            {loading
                                ? "Generating..."
                                : "Generate Report"}
                        </button>

                        <button
                            type="button"
                            className="reset-report-button"
                            onClick={
                                handleResetFilters
                            }
                            disabled={loading}
                        >
                            ↻ Reset
                        </button>

                    </div>

                    {validationMessage && (
                        <div className="report-validation-message">
                            ⚠️ {validationMessage}
                        </div>
                    )}

                    {errorMessage && (
                        <div className="report-error">
                            ❌ {errorMessage}
                        </div>
                    )}

                </section>

                {/* ========================================
                    STATISTICS
                ======================================== */}

                {report && (
                    <section className="report-statistics">

                        <div className="report-stat-card">

                            <div className="report-stat-icon">
                                📄
                            </div>

                            <div>
                                <span>
                                    Total Documents
                                </span>

                                <strong>
                                    {report.summary?.totalDocuments ?? 0}
                                </strong>
                            </div>

                        </div>

                        <div className="report-stat-card">

                            <div className="report-stat-icon">
                                ⏳
                            </div>

                            <div>
                                <span>
                                    Pending
                                </span>

                                <strong>
                                    {report.summary?.pendingDocuments ?? 0}
                                </strong>
                            </div>

                        </div>

                        <div className="report-stat-card">

                            <div className="report-stat-icon">
                                ✅
                            </div>

                            <div>
                                <span>
                                    Approved
                                </span>

                                <strong>
                                    {report.summary?.approvedDocuments ?? 0}
                                </strong>
                            </div>

                        </div>

                        <div className="report-stat-card">

                            <div className="report-stat-icon">
                                ❌
                            </div>

                            <div>
                                <span>
                                    Rejected
                                </span>

                                <strong>
                                    {report.summary?.rejectedDocuments ?? 0}
                                </strong>
                            </div>

                        </div>

                        <div className="report-stat-card">

                            <div className="report-stat-icon">
                                💰
                            </div>

                            <div>
                                <span>
                                    Total Spend
                                </span>

                                <strong>
                                    {formatMoney(
                                        report.summary?.totalAmount
                                    )}
                                </strong>
                            </div>

                        </div>

                        <div className="report-stat-card">

                            <div className="report-stat-icon">
                                🧾
                            </div>

                            <div>
                                <span>
                                    Total VAT
                                </span>

                                <strong>
                                    {formatMoney(
                                        report.summary?.totalVAT
                                    )}
                                </strong>
                            </div>

                        </div>

                        <div className="report-stat-card">

                            <div className="report-stat-icon">
                                💵
                            </div>

                            <div>
                                <span>
                                    Including VAT
                                </span>

                                <strong>
                                    {formatMoney(
                                        report.summary
                                            ?.totalAmountIncludingVAT
                                    )}
                                </strong>
                            </div>

                        </div>

                    </section>
                )}

                {/* ========================================
                    ANALYTICS
                ======================================== */}

                {report && (
                    <section className="report-analytics-section">

                        <div className="section-heading">

                            <div>
                                <h2>
                                    Analytics Overview
                                </h2>

                                <p>
                                    Visual overview of spending and
                                    approval activity
                                </p>
                            </div>

                        </div>

                        <div className="report-analytics-grid">

                            {/* VENDOR SPENDING */}

                            <div className="report-chart-card">

                                <h3>
                                    Spending by Vendor
                                </h3>

                                <p>
                                    Top vendors based on total invoice
                                    value
                                </p>

                                {vendorAnalysis.length === 0 ? (
                                    <div className="report-empty-state">
                                        <div>
                                            🏢
                                        </div>

                                        <h3>
                                            No vendor data
                                        </h3>

                                        <p>
                                            No vendor information is
                                            available.
                                        </p>
                                    </div>
                                ) : (
                                    <div className="vendor-chart-list">

                                        {vendorAnalysis
                                            .slice(0, 5)
                                            .map((item) => {

                                                const percentage =
                                                    analytics.maximumVendorTotal > 0
                                                        ? (
                                                            item.total /
                                                            analytics.maximumVendorTotal
                                                        ) * 100
                                                        : 0;

                                                return (
                                                    <div
                                                        className="vendor-chart-row"
                                                        key={item.vendor}
                                                    >

                                                        <div className="vendor-chart-header">

                                                            <span className="vendor-chart-name">
                                                                {item.vendor}
                                                            </span>

                                                            <span className="vendor-chart-value">
                                                                {formatMoney(
                                                                    item.total
                                                                )}
                                                            </span>

                                                        </div>

                                                        <div className="vendor-chart-track">

                                                            <div
                                                                className="vendor-chart-bar"
                                                                style={{
                                                                    width: `${percentage}%`
                                                                }}
                                                            />

                                                        </div>

                                                    </div>
                                                );
                                            })}

                                    </div>
                                )}

                            </div>

                            {/* APPROVAL STATUS */}

                            <div className="report-chart-card">

                                <h3>
                                    Approval Status
                                </h3>

                                <p>
                                    Current document approval breakdown
                                </p>

                                <div className="status-chart-list">

                                    <div className="status-chart-row">

                                        <span className="status-chart-label">
                                            Pending
                                        </span>

                                        <div className="status-chart-track">

                                            <div
                                                className="status-chart-bar pending"
                                                style={{
                                                    width:
                                                        report.summary?.totalDocuments > 0
                                                            ? `${(
                                                                analytics.pending /
                                                                report.summary.totalDocuments
                                                            ) * 100}%`
                                                            : "0%"
                                                }}
                                            />

                                        </div>

                                        <span className="status-chart-count">
                                            {analytics.pending}
                                        </span>

                                    </div>

                                    <div className="status-chart-row">

                                        <span className="status-chart-label">
                                            Approved
                                        </span>

                                        <div className="status-chart-track">

                                            <div
                                                className="status-chart-bar approved"
                                                style={{
                                                    width:
                                                        report.summary?.totalDocuments > 0
                                                            ? `${(
                                                                analytics.approved /
                                                                report.summary.totalDocuments
                                                            ) * 100}%`
                                                            : "0%"
                                                }}
                                            />

                                        </div>

                                        <span className="status-chart-count">
                                            {analytics.approved}
                                        </span>

                                    </div>

                                    <div className="status-chart-row">

                                        <span className="status-chart-label">
                                            Rejected
                                        </span>

                                        <div className="status-chart-track">

                                            <div
                                                className="status-chart-bar rejected"
                                                style={{
                                                    width:
                                                        report.summary?.totalDocuments > 0
                                                            ? `${(
                                                                analytics.rejected /
                                                                report.summary.totalDocuments
                                                            ) * 100}%`
                                                            : "0%"
                                                }}
                                            />

                                        </div>

                                        <span className="status-chart-count">
                                            {analytics.rejected}
                                        </span>

                                    </div>

                                </div>

                            </div>

                        </div>

                    </section>
                )}

                {/* ========================================
                    SPEND SUMMARY
                ======================================== */}

                {report &&
                    reportType === "spend" && (
                        <section className="report-table-section">

                            <div className="section-heading">

                                <div>
                                    <h2>
                                        Spend Summary
                                    </h2>

                                    <p>
                                        Overview of document spending and
                                        approval status
                                    </p>
                                </div>

                            </div>

                            <div className="report-summary-panel">

                                <div>
                                    <span>
                                        Total Spend
                                    </span>

                                    <strong>
                                        {formatMoney(
                                            report.summary?.totalAmount
                                        )}
                                    </strong>
                                </div>

                                <div>
                                    <span>
                                        VAT
                                    </span>

                                    <strong>
                                        {formatMoney(
                                            report.summary?.totalVAT
                                        )}
                                    </strong>
                                </div>

                                <div>
                                    <span>
                                        Total Including VAT
                                    </span>

                                    <strong>
                                        {formatMoney(
                                            report.summary
                                                ?.totalAmountIncludingVAT
                                        )}
                                    </strong>
                                </div>

                            </div>

                        </section>
                    )}

                {/* ========================================
                    VENDOR ANALYSIS
                ======================================== */}

                {report &&
                    reportType === "vendor" && (
                        <section className="report-table-section">

                            <div className="section-heading">

                                <div>
                                    <h2>
                                        Vendor Analysis
                                    </h2>

                                    <p>
                                        Spending breakdown by vendor
                                    </p>
                                </div>

                            </div>

                            {vendorAnalysis.length === 0 ? (
                                <div className="report-empty-state">

                                    <div>
                                        🏢
                                    </div>

                                    <h3>
                                        No vendor data
                                    </h3>

                                    <p>
                                        No vendor information is available
                                        for the selected filters.
                                    </p>

                                </div>
                            ) : (
                                <div className="report-table-wrapper">

                                    <table className="report-table">

                                        <thead>

                                            <tr>
                                                <th>
                                                    Vendor
                                                </th>

                                                <th>
                                                    Invoices
                                                </th>

                                                <th>
                                                    Amount
                                                </th>

                                                <th>
                                                    VAT
                                                </th>

                                                <th>
                                                    Total
                                                </th>
                                            </tr>

                                        </thead>

                                        <tbody>

                                            {vendorAnalysis.map(
                                                (item) => (
                                                    <tr
                                                        key={
                                                            item.vendor
                                                        }
                                                    >

                                                        <td>
                                                            {
                                                                item.vendor
                                                            }
                                                        </td>

                                                        <td>
                                                            {
                                                                item.invoices
                                                            }
                                                        </td>

                                                        <td>
                                                            {formatMoney(
                                                                item.amount
                                                            )}
                                                        </td>

                                                        <td>
                                                            {formatMoney(
                                                                item.vat
                                                            )}
                                                        </td>

                                                        <td>
                                                            {formatMoney(
                                                                item.total
                                                            )}
                                                        </td>

                                                    </tr>
                                                )
                                            )}

                                        </tbody>

                                    </table>

                                </div>
                            )}

                        </section>
                    )}

                {/* ========================================
                    TAX / VAT REPORT
                ======================================== */}

                {report &&
                    reportType === "tax" && (
                        <section className="report-table-section">

                            <div className="section-heading">

                                <div>
                                    <h2>
                                        Tax / VAT Report
                                    </h2>

                                    <p>
                                        Invoice amounts and VAT information
                                    </p>
                                </div>

                            </div>

                            <div className="report-table-wrapper">

                                <table className="report-table">

                                    <thead>

                                        <tr>

                                            <th>
                                                Vendor
                                            </th>

                                            <th>
                                                Invoice Number
                                            </th>

                                            <th>
                                                Invoice Date
                                            </th>

                                            <th>
                                                Amount
                                            </th>

                                            <th>
                                                VAT
                                            </th>

                                            <th>
                                                Total
                                            </th>

                                        </tr>

                                    </thead>

                                    <tbody>

                                        {report.documents?.map(
                                            (document) => (
                                                <tr
                                                    key={
                                                        document.id
                                                    }
                                                >

                                                    <td>
                                                        {
                                                            document.vendor ||
                                                            "-"
                                                        }
                                                    </td>

                                                    <td>
                                                        {
                                                            document.invoiceNumber ||
                                                            "-"
                                                        }
                                                    </td>

                                                    <td>
                                                        {formatDate(
                                                            document.invoiceDate
                                                        )}
                                                    </td>

                                                    <td>
                                                        {formatMoney(
                                                            document.amount
                                                        )}
                                                    </td>

                                                    <td>
                                                        {formatMoney(
                                                            document.vat
                                                        )}
                                                    </td>

                                                    <td>
                                                        {formatMoney(
                                                            document.totalAmount
                                                        )}
                                                    </td>

                                                </tr>
                                            )
                                        )}

                                    </tbody>

                                </table>

                            </div>

                        </section>
                    )}

                {/* ========================================
                    EXPORT
                ======================================== */}

                <section className="report-export-section">

                    <div>

                        <h2>
                            Export Report
                        </h2>

                        <p>
                            Download the current filtered report
                        </p>

                    </div>

                    <div className="report-export-actions">

                        <button
                            type="button"
                            className="export-excel-button"
                            onClick={
                                handleExportExcel
                            }
                            disabled={exporting}
                        >
                            📊{" "}
                            {exporting
                                ? "Exporting..."
                                : "Export Excel"}
                        </button>

                        <button
                            type="button"
                            className="export-pdf-button"
                            onClick={
                                handleExportPdf
                            }
                            disabled={exportingPdf}
                        >
                            📄{" "}
                            {exportingPdf
                                ? "Exporting..."
                                : "Export PDF"}
                        </button>

                    </div>

                </section>

            </main>

        </div>
    );
}

export default Reports;

