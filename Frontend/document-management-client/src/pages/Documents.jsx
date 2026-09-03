
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

import { getUser, logout } from "../utils/auth";
import api from "../services/api";

import "./Documents.css";

function Documents() {
    const navigate = useNavigate();
    const user = getUser();

    // ========================================
    // STATE
    // ========================================

    const [documents, setDocuments] = useState([]);
    const [loading, setLoading] = useState(true);

    const [searchTerm, setSearchTerm] = useState("");
    const [statusFilter, setStatusFilter] = useState("All");

    const [selectedFile, setSelectedFile] = useState(null);
    const [description, setDescription] = useState("");

    const [uploading, setUploading] = useState(false);
    const [deletingId, setDeletingId] = useState(null);

    const [uploadError, setUploadError] = useState("");
    const [uploadSuccess, setUploadSuccess] = useState("");

    // ========================================
    // AUTHORIZATION POPUP
    // ========================================

    const [authorizationMessage, setAuthorizationMessage] =
        useState("");

    const showAuthorizationPopup = (message) => {
        setAuthorizationMessage(message);
    };

    const closeAuthorizationPopup = () => {
        setAuthorizationMessage("");
    };

    // ========================================
    // LOAD DOCUMENTS
    // ========================================

    useEffect(() => {
        const loadDocuments = async () => {
            try {
                setLoading(true);

                const response = await api.get("/Documents");

                setDocuments(response.data);
            } catch (error) {
                console.error(
                    "Error loading documents:",
                    error
                );

                if (error.response?.status === 401) {
                    logout();
                    navigate("/login");
                    return;
                }

                if (error.response?.status === 403) {
                    showAuthorizationPopup(
                        "You do not have authorization to view documents."
                    );
                    return;
                }

                setUploadError(
                    "Failed to load documents."
                );
            } finally {
                setLoading(false);
            }
        };

        loadDocuments();
    }, [navigate]);

    // ========================================
    // HANDLE FILE SELECTION
    // ========================================

    const handleFileChange = (event) => {
        const file = event.target.files?.[0];

        setUploadError("");
        setUploadSuccess("");

        if (!file) {
            setSelectedFile(null);
            return;
        }

        setSelectedFile(file);
    };

    // ========================================
    // UPLOAD DOCUMENT
    // ========================================

    const handleUpload = async (event) => {
        event.preventDefault();

        setUploadError("");
        setUploadSuccess("");

        if (!selectedFile) {
            setUploadError(
                "Please select a file to upload."
            );
            return;
        }

        try {
            setUploading(true);

            const formData = new FormData();

            formData.append(
                "file",
                selectedFile
            );

            formData.append(
                "description",
                description
            );

            const response = await api.post(
                "/Documents/upload",
                formData,
                {
                    headers: {
                        "Content-Type":
                            "multipart/form-data",
                    },
                }
            );

            setUploadSuccess(
                response.data?.message ||
                    "Document uploaded successfully."
            );

            setSelectedFile(null);
            setDescription("");

            const fileInput =
                document.getElementById(
                    "documentFile"
                );

            if (fileInput) {
                fileInput.value = "";
            }

            const documentsResponse =
                await api.get("/Documents");

            setDocuments(
                documentsResponse.data
            );
        } catch (error) {
            console.error(
                "Upload error:",
                error
            );

            if (error.response?.status === 401) {
                logout();
                navigate("/login");
                return;
            }

            if (error.response?.status === 403) {
                showAuthorizationPopup(
                    "You do not have authorization to upload documents."
                );
                return;
            }

            setUploadError(
                error.response?.data?.message ||
                    "Failed to upload the document."
            );
        } finally {
            setUploading(false);
        }
    };

    // ========================================
    // VIEW DOCUMENT
    // ========================================

    const handleViewDocument = async (id) => {
        try {
            const response = await api.get(
                `/Documents/${id}/view`,
                {
                    responseType: "blob",
                }
            );

            const contentType =
                response.headers["content-type"] ||
                "application/pdf";

            const fileBlob = new Blob(
                [response.data],
                {
                    type: contentType,
                }
            );

            const url =
                window.URL.createObjectURL(
                    fileBlob
                );

            const link =
                document.createElement("a");

            link.href = url;
            link.target = "_blank";
            link.rel = "noopener noreferrer";

            document.body.appendChild(link);

            link.click();

            link.remove();

            setTimeout(() => {
                window.URL.revokeObjectURL(url);
            }, 60000);

        } catch (error) {
            console.error(
                "View error:",
                error
            );

            if (error.response?.status === 401) {
                logout();
                navigate("/login");
                return;
            }

            if (error.response?.status === 403) {
                showAuthorizationPopup(
                    "You do not have authorization to view this document."
                );
                return;
            }

            alert(
                "Failed to view the document."
            );
        }
    };

    // ========================================
    // DOWNLOAD DOCUMENT
    // ========================================

    const handleDownloadDocument = async (
        id,
        fileName
    ) => {
        try {
            const response = await api.get(
                `/Documents/${id}/download`,
                {
                    responseType: "blob",
                }
            );

            const contentType =
                response.headers[
                    "content-type"
                ] || "application/octet-stream";

            const fileBlob = new Blob(
                [response.data],
                {
                    type: contentType,
                }
            );

            const url =
                window.URL.createObjectURL(
                    fileBlob
                );

            const link =
                document.createElement("a");

            link.href = url;

            link.download =
                fileName || "document";

            document.body.appendChild(link);

            link.click();

            link.remove();

            window.URL.revokeObjectURL(
                url
            );
        } catch (error) {
            console.error(
                "Download error:",
                error
            );

            if (error.response?.status === 401) {
                logout();
                navigate("/login");
                return;
            }

            if (error.response?.status === 403) {
                showAuthorizationPopup(
                    "You do not have authorization to download documents."
                );
                return;
            }

            alert(
                "Failed to download the document."
            );
        }
    };

    // ========================================
    // DELETE DOCUMENT
    // ========================================

    const handleDeleteDocument = async (
        id,
        fileName
    ) => {
        const confirmed = window.confirm(
            `Are you sure you want to delete "${fileName}"?\n\nThis action cannot be undone.`
        );

        if (!confirmed) {
            return;
        }

        try {
            setDeletingId(id);

            setUploadError("");
            setUploadSuccess("");

            const response = await api.delete(
                `/Documents/${id}`
            );

            setDocuments((currentDocuments) =>
                currentDocuments.filter(
                    (document) =>
                        document.id !== id
                )
            );

            setUploadSuccess(
                response.data?.message ||
                    "Document deleted successfully."
            );
        } catch (error) {
            console.error(
                "Delete error:",
                error
            );

            if (error.response?.status === 401) {
                logout();
                navigate("/login");
                return;
            }

            if (error.response?.status === 403) {
                showAuthorizationPopup(
                    "You do not have authorization to delete documents."
                );
                return;
            }

            setUploadError(
                error.response?.data?.message ||
                    "Failed to delete the document."
            );
        } finally {
            setDeletingId(null);
        }
    };

    // ========================================
    // LOGOUT
    // ========================================

    const handleLogout = () => {
        logout();
        navigate("/login");
    };

    // ========================================
    // FORMAT FILE SIZE
    // ========================================

    const formatFileSize = (bytes) => {
        if (
            bytes === null ||
            bytes === undefined ||
            bytes === 0
        ) {
            return "0 Bytes";
        }

        const sizes = [
            "Bytes",
            "KB",
            "MB",
            "GB",
        ];

        const i = Math.floor(
            Math.log(bytes) /
                Math.log(1024)
        );

        return (
            parseFloat(
                (
                    bytes /
                    Math.pow(1024, i)
                ).toFixed(2)
            ) +
            " " +
            sizes[i]
        );
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
                day: "numeric",
            }
        );
    };

    // ========================================
    // FORMAT MONEY
    // ========================================

    const formatMoney = (amount) => {
        if (
            amount === null ||
            amount === undefined
        ) {
            return "-";
        }

        return `R ${Number(amount).toLocaleString(
            "en-ZA",
            {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2,
            }
        )}`;
    };

    // ========================================
    // FILTER DOCUMENTS
    // ========================================

    const filteredDocuments =
        documents.filter(
            (document) => {
                const search =
                    searchTerm
                        .toLowerCase()
                        .trim();

                const invoiceData =
                    document.invoiceData;

                const matchesStatus =
                    statusFilter ===
                        "All" ||
                    document.status
                        ?.toLowerCase() ===
                        statusFilter.toLowerCase();

                const matchesSearch =
                    !search ||
                    document.fileName
                        ?.toLowerCase()
                        .includes(search) ||
                    document.description
                        ?.toLowerCase()
                        .includes(search) ||
                    document.uploadedBy
                        ?.toLowerCase()
                        .includes(search) ||
                    document.fileType
                        ?.toLowerCase()
                        .includes(search) ||
                    document.status
                        ?.toLowerCase()
                        .includes(search) ||
                    invoiceData?.invoiceNumber
                        ?.toLowerCase()
                        .includes(search) ||
                    invoiceData?.vendor
                        ?.toLowerCase()
                        .includes(search) ||
                    invoiceData?.documentType
                        ?.toLowerCase()
                        .includes(search);

                return (
                    matchesStatus &&
                    matchesSearch
                );
            }
        );

    // ========================================
    // DOCUMENT STATISTICS
    // ========================================

    const totalDocuments =
        documents.length;

    const pendingDocuments =
        documents.filter(
            (document) =>
                document.status
                    ?.toLowerCase() ===
                "pending"
        ).length;

    const approvedDocuments =
        documents.filter(
            (document) =>
                document.status
                    ?.toLowerCase() ===
                "approved"
        ).length;

    const rejectedDocuments =
        documents.filter(
            (document) =>
                document.status
                    ?.toLowerCase() ===
                "rejected"
        ).length;

    // ========================================
    // STATUS CLASS
    // ========================================

    const getStatusClass = (status) => {
        switch (
            status?.toLowerCase()
        ) {
            case "approved":
                return "status-approved";

            case "rejected":
                return "status-rejected";

            case "pending":
                return "status-pending";

            default:
                return "status-pending";
        }
    };

    // ========================================
    // EXTRACTION STATUS CLASS
    // ========================================

    const getExtractionStatusClass = (
        status
    ) => {
        switch (
            status?.toLowerCase()
        ) {
            case "completed":
                return "extraction-completed";

            case "failed":
                return "extraction-failed";

            case "pending":
            default:
                return "extraction-pending";
        }
    };

    // ========================================
    // RENDER
    // ========================================

    return (
        <div className="documents-page">

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
                            navigate(
                                "/dashboard"
                            )
                        }
                    >
                        <span>
                            📊
                        </span>

                        Dashboard
                    </button>

                    <button
                        type="button"
                        className="nav-item active"
                    >
                        <span>
                            📁
                        </span>

                        Documents
                    </button>

                    <button
                        type="button"
                        className="nav-item"
                        onClick={() =>
                            navigate(
                                "/reports"
                            )
                        }
                    >
                        <span>
                            📊
                        </span>

                        Reports
                    </button>

                    {/* AI INSIGHTS */}
                    <button
                        type="button"
                        className="nav-item"
                        onClick={() =>
                            navigate(
                                "/ai-insights"
                            )
                        }
                    >
                        <span>
                            🤖
                        </span>

                        AI Insights
                    </button>

                    <button
                        type="button"
                        className="nav-item"
                        onClick={() =>
                            navigate(
                                "/approval-workflow"
                            )
                        }
                    >
                        <span>
                            🔐
                        </span>

                        Approval Workflow
                    </button>

                </nav>

                <div className="sidebar-bottom">

                    <button
                        type="button"
                        className="nav-item logout-item"
                        onClick={
                            handleLogout
                        }
                    >
                        <span>
                            🚪
                        </span>

                        Logout
                    </button>

                </div>

            </aside>

            {/* ========================================
                MAIN CONTENT
            ======================================== */}

            <main className="main-content">

                <header className="page-header">

                    <div>
                        <h1>
                            Documents
                        </h1>

                        <p>
                            Manage and view
                            your documents
                        </p>
                    </div>

                    <div className="user-info">

                        <div className="user-avatar">

                            {user?.fullName
                                ?.charAt(0)
                                ?.toUpperCase() ||
                                "U"}

                        </div>

                        <div className="user-details">

                            <strong>
                                {user?.fullName ||
                                    "User"}
                            </strong>

                            <span>
                                {user?.role ||
                                    "User"}
                            </span>

                        </div>

                    </div>

                </header>

                {/* ========================================
                    DOCUMENT STATISTICS
                ======================================== */}

                <section className="document-statistics">

                    <div className="document-stat-card">

                        <div className="stat-icon">
                            📄
                        </div>

                        <div>
                            <span>
                                Total Documents
                            </span>

                            <strong>
                                {
                                    totalDocuments
                                }
                            </strong>
                        </div>

                    </div>

                    <div className="document-stat-card">

                        <div className="stat-icon">
                            ⏳
                        </div>

                        <div>
                            <span>
                                Pending
                            </span>

                            <strong>
                                {
                                    pendingDocuments
                                }
                            </strong>
                        </div>

                    </div>

                    <div className="document-stat-card">

                        <div className="stat-icon">
                            ✅
                        </div>

                        <div>
                            <span>
                                Approved
                            </span>

                            <strong>
                                {
                                    approvedDocuments
                                }
                            </strong>
                        </div>

                    </div>

                    <div className="document-stat-card">

                        <div className="stat-icon">
                            ❌
                        </div>

                        <div>
                            <span>
                                Rejected
                            </span>

                            <strong>
                                {
                                    rejectedDocuments
                                }
                            </strong>
                        </div>

                    </div>

                </section>

                {/* ========================================
                    UPLOAD SECTION
                ======================================== */}

                <section className="upload-section">

                    <div className="section-heading">

                        <div>
                            <h2>
                                Upload Document
                            </h2>

                            <p>
                                Add a new
                                document to
                                the system
                            </p>
                        </div>

                    </div>

                    <form
                        className="upload-form"
                        onSubmit={
                            handleUpload
                        }
                    >

                        <div className="form-group">

                            <label htmlFor="documentFile">
                                Select File
                            </label>

                            <input
                                id="documentFile"
                                type="file"
                                onChange={
                                    handleFileChange
                                }
                            />

                            {selectedFile && (

                                <div className="selected-file">

                                    <span>
                                        📎
                                    </span>

                                    <div>

                                        <strong>
                                            {
                                                selectedFile.name
                                            }
                                        </strong>

                                        <small>
                                            {formatFileSize(
                                                selectedFile.size
                                            )}
                                        </small>

                                    </div>

                                </div>

                            )}

                        </div>

                        <div className="form-group">

                            <label htmlFor="description">
                                Description
                            </label>

                            <textarea
                                id="description"
                                value={
                                    description
                                }
                                onChange={(
                                    event
                                ) =>
                                    setDescription(
                                        event.target
                                            .value
                                    )
                                }
                                placeholder="Enter a description for the document..."
                                rows="4"
                            />

                        </div>

                        {uploadError && (

                            <div className="upload-error">

                                ❌{" "}

                                {
                                    uploadError
                                }

                            </div>

                        )}

                        {uploadSuccess && (

                            <div className="upload-success">

                                ✅{" "}

                                {
                                    uploadSuccess
                                }

                            </div>

                        )}

                        <button
                            type="submit"
                            className="upload-button"
                            disabled={
                                uploading
                            }
                        >
                            {uploading
                                ? "Uploading..."
                                : "📤 Upload Document"}
                        </button>

                    </form>

                </section>

                {/* ========================================
                    DOCUMENT LIST
                ======================================== */}

                <section className="documents-section">

                    <div className="section-heading">

                        <div>
                            <h2>
                                Document List
                            </h2>

                            <p>
                                View, manage and
                                review extracted
                                document information
                            </p>
                        </div>

                    </div>

                    {/* SEARCH */}

                    <div className="document-search">

                        <input
                            type="text"
                            placeholder="Search documents, invoice number, vendor..."
                            value={
                                searchTerm
                            }
                            onChange={(
                                event
                            ) =>
                                setSearchTerm(
                                    event.target
                                        .value
                                )
                            }
                        />

                    </div>

                    {/* STATUS FILTERS */}

                    <div className="document-filters">

                        <button
                            type="button"
                            className={
                                statusFilter ===
                                "All"
                                    ? "active"
                                    : ""
                            }
                            onClick={() =>
                                setStatusFilter(
                                    "All"
                                )
                            }
                        >
                            All
                        </button>

                        <button
                            type="button"
                            className={
                                statusFilter ===
                                "Pending"
                                    ? "active"
                                    : ""
                            }
                            onClick={() =>
                                setStatusFilter(
                                    "Pending"
                                )
                            }
                        >
                            Pending
                        </button>

                        <button
                            type="button"
                            className={
                                statusFilter ===
                                "Approved"
                                    ? "active"
                                    : ""
                            }
                            onClick={() =>
                                setStatusFilter(
                                    "Approved"
                                )
                            }
                        >
                            Approved
                        </button>

                        <button
                            type="button"
                            className={
                                statusFilter ===
                                "Rejected"
                                    ? "active"
                                    : ""
                            }
                            onClick={() =>
                                setStatusFilter(
                                    "Rejected"
                                )
                            }
                        >
                            Rejected
                        </button>

                    </div>

                    {/* DOCUMENT TABLE */}

                    <div className="documents-table-container">

                        {loading ? (

                            <div className="empty-state">

                                <div className="empty-icon">
                                    ⏳
                                </div>

                                <h3>
                                    Loading documents...
                                </h3>

                            </div>

                        ) : filteredDocuments.length === 0 ? (

                            <div className="empty-state">

                                <div className="empty-icon">
                                    📂
                                </div>

                                <h3>
                                    No documents
                                    found
                                </h3>

                                <p>

                                    {searchTerm
                                        ? "Try changing your search or filter."
                                        : "Upload a document to get started."}

                                </p>

                                {searchTerm && (

                                    <button
                                        type="button"
                                        className="clear-search-button"
                                        onClick={() => {

                                            setSearchTerm(
                                                ""
                                            );

                                            setStatusFilter(
                                                "All"
                                            );

                                        }}
                                    >
                                        Clear Search
                                    </button>

                                )}

                            </div>

                        ) : (

                            <div className="table-wrapper">

                                <table className="documents-table">

                                    <thead>

                                        <tr>

                                            <th>
                                                File Name
                                            </th>

                                            <th>
                                                Description
                                            </th>

                                            <th>
                                                Extracted Information
                                            </th>

                                            <th>
                                                Type
                                            </th>

                                            <th>
                                                Size
                                            </th>

                                            <th>
                                                Uploaded By
                                            </th>

                                            <th>
                                                Status
                                            </th>

                                            <th>
                                                Date
                                            </th>

                                            <th>
                                                Actions
                                            </th>

                                        </tr>

                                    </thead>

                                    <tbody>

                                        {filteredDocuments.map(
                                            (
                                                document
                                            ) => {

                                                const invoiceData =
                                                    document.invoiceData;

                                                return (

                                                    <tr
                                                        key={
                                                            document.id
                                                        }
                                                    >

                                                        <td>

                                                            <div className="file-name">

                                                                <span className="file-icon">
                                                                    📄
                                                                </span>

                                                                <span
                                                                    title={
                                                                        document.fileName
                                                                    }
                                                                >
                                                                    {
                                                                        document.fileName
                                                                    }
                                                                </span>

                                                            </div>

                                                        </td>

                                                        <td>

                                                            <span
                                                                title={
                                                                    document.description ||
                                                                    ""
                                                                }
                                                            >
                                                                {
                                                                    document.description ||
                                                                    "No description"
                                                                }
                                                            </span>

                                                        </td>

                                                        <td>

                                                            {invoiceData ? (

                                                                <div className="extracted-information">

                                                                    <div className="extracted-row">

                                                                        <strong>
                                                                            Document:
                                                                        </strong>

                                                                        <span>
                                                                            {
                                                                                invoiceData.documentType ||
                                                                                "-"
                                                                            }
                                                                        </span>

                                                                    </div>

                                                                    <div className="extracted-row">

                                                                        <strong>
                                                                            Invoice No:
                                                                        </strong>

                                                                        <span>
                                                                            {
                                                                                invoiceData.invoiceNumber ||
                                                                                "-"
                                                                            }
                                                                        </span>

                                                                    </div>

                                                                    <div className="extracted-row">

                                                                        <strong>
                                                                            Vendor:
                                                                        </strong>

                                                                        <span
                                                                            title={
                                                                                invoiceData.vendor ||
                                                                                ""
                                                                            }
                                                                        >
                                                                            {
                                                                                invoiceData.vendor ||
                                                                                "-"
                                                                            }
                                                                        </span>

                                                                    </div>

                                                                    <div className="extracted-row">

                                                                        <strong>
                                                                            Date:
                                                                        </strong>

                                                                        <span>
                                                                            {formatDate(
                                                                                invoiceData.invoiceDate
                                                                            )}
                                                                        </span>

                                                                    </div>

                                                                    <div className="extracted-row">

                                                                        <strong>
                                                                            Amount:
                                                                        </strong>

                                                                        <span>
                                                                            {formatMoney(
                                                                                invoiceData.amount
                                                                            )}
                                                                        </span>

                                                                    </div>

                                                                    <div className="extracted-row">

                                                                        <strong>
                                                                            VAT:
                                                                        </strong>

                                                                        <span>
                                                                            {formatMoney(
                                                                                invoiceData.vat
                                                                            )}
                                                                        </span>

                                                                    </div>

                                                                    <div className="extracted-row">

                                                                        <strong>
                                                                            Total:
                                                                        </strong>

                                                                        <span>
                                                                            {formatMoney(
                                                                                invoiceData.totalAmount
                                                                            )}
                                                                        </span>

                                                                    </div>

                                                                    <div className="extraction-status-row">

                                                                        <span>
                                                                            Extraction:
                                                                        </span>

                                                                        <span
                                                                            className={`extraction-status ${getExtractionStatusClass(
                                                                                invoiceData.extractionStatus
                                                                            )}`}
                                                                        >
                                                                            {
                                                                                invoiceData.extractionStatus ||
                                                                                "Pending"
                                                                            }
                                                                        </span>

                                                                    </div>

                                                                </div>

                                                            ) : (

                                                                <span className="no-extracted-data">
                                                                    No extracted data
                                                                </span>

                                                            )}

                                                        </td>

                                                        <td>
                                                            {
                                                                document.fileType ||
                                                                "Unknown"
                                                            }
                                                        </td>

                                                        <td>
                                                            {formatFileSize(
                                                                document.fileSize
                                                            )}
                                                        </td>

                                                        <td>
                                                            {
                                                                document.uploadedBy ||
                                                                "Unknown"
                                                            }
                                                        </td>

                                                        <td>

                                                            <span
                                                                className={`status-badge ${getStatusClass(
                                                                    document.status
                                                                )}`}
                                                            >
                                                                {
                                                                    document.status
                                                                }
                                                            </span>

                                                        </td>

                                                        <td>
                                                            {formatDate(
                                                                document.uploadedAt
                                                            )}
                                                        </td>

                                                        <td>

                                                            <div className="document-actions">

                                                                <button
                                                                    type="button"
                                                                    className="view-button"
                                                                    onClick={() =>
                                                                        handleViewDocument(
                                                                            document.id
                                                                        )
                                                                    }
                                                                >
                                                                    👁 View
                                                                </button>

                                                                <button
                                                                    type="button"
                                                                    className="download-button"
                                                                    onClick={() =>
                                                                        handleDownloadDocument(
                                                                            document.id,
                                                                            document.fileName
                                                                        )
                                                                    }
                                                                >
                                                                    ⬇ Download
                                                                </button>

                                                                <button
                                                                    type="button"
                                                                    className="delete-button"
                                                                    onClick={() =>
                                                                        handleDeleteDocument(
                                                                            document.id,
                                                                            document.fileName
                                                                        )
                                                                    }
                                                                    disabled={
                                                                        deletingId ===
                                                                        document.id
                                                                    }
                                                                >
                                                                    {deletingId ===
                                                                    document.id
                                                                        ? "Deleting..."
                                                                        : "🗑 Delete"}
                                                                </button>

                                                            </div>

                                                        </td>

                                                    </tr>

                                                );
                                            }
                                        )}

                                    </tbody>

                                </table>

                            </div>

                        )}

                    </div>

                </section>

            </main>

        </div>
    );
}

export default Documents;

