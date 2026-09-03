
import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

import { getUser, logout } from "../utils/auth";
import api from "../services/api";

import "./ApprovalWorkflow.css";

function ApprovalWorkflow() {
  const navigate = useNavigate();
  const user = getUser();

  const [documents, setDocuments] = useState([]);
  const [approvals, setApprovals] = useState({});
  const [loading, setLoading] = useState(true);

  const [selectedDocument, setSelectedDocument] = useState(null);
  const [comments, setComments] = useState("");
  const [actionLoading, setActionLoading] = useState(false);

  const [message, setMessage] = useState("");
  const [errorMessage, setErrorMessage] = useState("");

  // =========================================================
  // FETCH WORKFLOW DATA
  // =========================================================

  const fetchWorkflow = useCallback(async () => {
    const response = await api.get("/Documents");

    const documentList = response.data || [];

    const approvalResults = {};

    for (const document of documentList) {
      try {
        const approvalResponse = await api.get(
          `/Approvals/document/${document.id}`
        );

        approvalResults[document.id] = approvalResponse.data || [];
      } catch (error) {
        console.error(
          `Failed to load approvals for document ${document.id}:`,
          error
        );

        approvalResults[document.id] = [];
      }
    }

    return {
      documentList,
      approvalResults
    };
  }, []);

  // =========================================================
  // INITIAL LOAD
  // =========================================================

  useEffect(() => {
    let cancelled = false;

    const loadInitialWorkflow = async () => {
      try {
        const {
          documentList,
          approvalResults
        } = await fetchWorkflow();

        if (cancelled) {
          return;
        }

        setDocuments(documentList);
        setApprovals(approvalResults);
        setErrorMessage("");
      } catch (error) {
        if (cancelled) {
          return;
        }

        console.error("Failed to load approval workflow:", error);

        if (error.response?.status === 401) {
          logout();
          navigate("/login");
          return;
        }

        if (error.response?.status === 403) {
          setErrorMessage(
            "You are not authorized to access the approval workflow."
          );
          return;
        }

        setErrorMessage(
          "Unable to load the approval workflow. Please try again."
        );
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    loadInitialWorkflow();

    return () => {
      cancelled = true;
    };
  }, [fetchWorkflow, navigate]);

  // =========================================================
  // REFRESH WORKFLOW
  // =========================================================

  const loadWorkflow = async () => {
    try {
      setLoading(true);
      setErrorMessage("");

      const {
        documentList,
        approvalResults
      } = await fetchWorkflow();

      setDocuments(documentList);
      setApprovals(approvalResults);
    } catch (error) {
      console.error("Failed to refresh approval workflow:", error);

      if (error.response?.status === 401) {
        logout();
        navigate("/login");
        return;
      }

      if (error.response?.status === 403) {
        setErrorMessage(
          "You are not authorized to access the approval workflow."
        );
        return;
      }

      setErrorMessage(
        "Unable to load the approval workflow. Please try again."
      );
    } finally {
      setLoading(false);
    }
  };

  // =========================================================
  // LOGOUT
  // =========================================================

  const handleLogout = () => {
    logout();
    navigate("/login");
  };

  // =========================================================
  // APPROVAL HELPERS
  // =========================================================

  const getApprovalForStage = (documentId, stage) => {
    const documentApprovals = approvals[documentId] || [];

    return documentApprovals.find(
      (approval) => approval.stage === stage
    );
  };

  const getStageStatus = (documentId, stage) => {
    const approval = getApprovalForStage(documentId, stage);

    return approval?.status || "Pending";
  };

  const getStatusClass = (status) => {
    if (status === "Approved") {
      return "status-approved";
    }

    if (status === "Rejected") {
      return "status-rejected";
    }

    return "status-pending";
  };

  const getDocumentStatusClass = (status) => {
    if (status === "Approved") {
      return "status-approved";
    }

    if (status === "Rejected") {
      return "status-rejected";
    }

    return "status-pending";
  };

  // =========================================================
  // ROLE FOR EACH STAGE
  // =========================================================

  const getRoleForStage = (stage) => {
    switch (stage) {
      case 1:
        return "Reviewer";

      case 2:
        return "Manager";

      case 3:
        return "Finance";

      default:
        return "";
    }
  };

  // =========================================================
  // CURRENT APPROVAL STAGE
  // =========================================================

  const getCurrentStage = (documentId) => {
    const documentApprovals = approvals[documentId] || [];

    const pendingApproval = documentApprovals.find(
      (approval) => approval.status === "Pending"
    );

    return pendingApproval?.stage || 0;
  };

  // =========================================================
  // CHECK WHETHER CURRENT USER CAN REVIEW THE DOCUMENT
  // =========================================================

  const canApproveStage = (documentId, stage) => {
    if (!stage) {
      return false;
    }

    const approval = getApprovalForStage(documentId, stage);

    if (!approval || approval.status !== "Pending") {
      return false;
    }

    // Admin can act on every stage
    if (user?.role === "Admin") {
      return true;
    }

    // Reviewer can only act on Stage 1
    if (user?.role === "Reviewer" && stage === 1) {
      return true;
    }

    // Manager can only act on Stage 2
    if (user?.role === "Manager" && stage === 2) {
      return true;
    }

    // Finance can only act on Stage 3
    if (user?.role === "Finance" && stage === 3) {
      return true;
    }

    // Viewer cannot approve or reject
    return false;
  };

  // =========================================================
  // OPEN REVIEW MODAL
  // =========================================================

  const handleOpenApproval = (document) => {
    const currentStage = getCurrentStage(document.id);

    if (!canApproveStage(document.id, currentStage)) {
      return;
    }

    setSelectedDocument(document);
    setComments("");
    setMessage("");
    setErrorMessage("");
  };

  // =========================================================
  // CLOSE REVIEW MODAL
  // =========================================================

  const handleCloseApproval = () => {
    if (actionLoading) {
      return;
    }

    setSelectedDocument(null);
    setComments("");
    setMessage("");
    setErrorMessage("");
  };

  // =========================================================
  // APPROVE / REJECT DOCUMENT
  // =========================================================

  const handleApprovalAction = async (action) => {
    if (!selectedDocument) {
      return;
    }

    const currentStage = getCurrentStage(selectedDocument.id);
    const role = getRoleForStage(currentStage);

    if (!currentStage || !role) {
      setErrorMessage(
        "No active approval stage was found for this document."
      );
      return;
    }

    // Double-check frontend permission
    if (!canApproveStage(selectedDocument.id, currentStage)) {
      setErrorMessage(
        `You are not authorized to perform the ${role} approval stage.`
      );
      return;
    }

    try {
      setActionLoading(true);
      setMessage("");
      setErrorMessage("");

      // IMPORTANT:
      // The Document ID is part of the API URL.
      const endpoint =
        action === "Approved"
          ? `/Approvals/${selectedDocument.id}/approve`
          : `/Approvals/${selectedDocument.id}/reject`;

      await api.post(endpoint, {
        role: role,
        comments: comments.trim() || null
      });

      setMessage(
        action === "Approved"
          ? `${role} approval completed successfully.`
          : `${role} rejected the document.`
      );

      setComments("");

      // Refresh the workflow immediately
      const {
        documentList,
        approvalResults
      } = await fetchWorkflow();

      setDocuments(documentList);
      setApprovals(approvalResults);

      // Close the modal after the workflow has refreshed
      setTimeout(() => {
        setSelectedDocument(null);
        setMessage("");
      }, 800);
    } catch (error) {
      console.error("Approval action failed:", error);

      if (error.response?.status === 401) {
        logout();
        navigate("/login");
        return;
      }

      if (error.response?.status === 403) {
        setErrorMessage(
          "You are not authorized to perform this approval action."
        );
        return;
      }

      setErrorMessage(
        error.response?.data?.message ||
          error.response?.data ||
          "The approval action could not be completed."
      );
    } finally {
      setActionLoading(false);
    }
  };

  // =========================================================
  // DOCUMENT FILTERS
  // =========================================================

  const pendingDocuments = documents.filter(
    (document) =>
      document.status === "Pending" ||
      document.status === "Pending Manager" ||
      document.status === "Pending Finance"
  );

  const approvedDocuments = documents.filter(
    (document) => document.status === "Approved"
  );

  const rejectedDocuments = documents.filter(
    (document) => document.status === "Rejected"
  );

  // =========================================================
  // RENDER
  // =========================================================

  return (
    <div className="approval-page">

      {/* =====================================================
          SIDEBAR
      ====================================================== */}

      <aside className="approval-sidebar">

        <div className="sidebar-logo">
          <h2>DMS</h2>
          <p>Document Management</p>
        </div>

        <nav className="sidebar-navigation">

          <button
            className="nav-item"
            onClick={() => navigate("/dashboard")}
          >
            🏠 Dashboard
          </button>

          <button
            className="nav-item"
            onClick={() => navigate("/documents")}
          >
            📄 Documents
          </button>

          <button
            className="nav-item"
            onClick={() => navigate("/reports")}
          >
            📊 Reports
          </button>

          <button
            className="nav-item"
            onClick={() => navigate("/ai-insights")}
          >
            🤖 AI Insights
          </button>

          <button
            className="nav-item active"
            onClick={() => navigate("/approval-workflow")}
          >
            🔐 Approval Workflow
          </button>

        </nav>

        <button
          className="logout-button"
          onClick={handleLogout}
        >
          🚪 Logout
        </button>

      </aside>

      {/* =====================================================
          MAIN CONTENT
      ====================================================== */}

      <main className="approval-content">

        {/* Header */}

        <header className="approval-header">

          <div>
            <h1>Approval Workflow</h1>

            <p>
              Review and approve documents through the three-stage
              approval process.
            </p>
          </div>

          <div className="user-info">

            <div className="user-avatar">
              {(user?.fullName || "U")
                .charAt(0)
                .toUpperCase()}
            </div>

            <div>
              <strong>
                {user?.fullName || "User"}
              </strong>

              <span>
                {user?.role || "User"}
              </span>
            </div>

          </div>

        </header>

        {/* ===================================================
            ROLE INFORMATION
        ==================================================== */}

        {user?.role === "Reviewer" && (
          <div className="workflow-information">
            <h2>Reviewer Access</h2>
            <p>
              You can review and approve or reject documents at
              Stage 1.
            </p>
          </div>
        )}

        {user?.role === "Manager" && (
          <div className="workflow-information">
            <h2>Manager Access</h2>
            <p>
              You can review and approve or reject documents at
              Stage 2 after Reviewer approval.
            </p>
          </div>
        )}

        {user?.role === "Finance" && (
          <div className="workflow-information">
            <h2>Finance Access</h2>
            <p>
              You can provide the final approval or rejection at
              Stage 3 after Manager approval.
            </p>
          </div>
        )}

        {user?.role === "Admin" && (
          <div className="workflow-information">
            <h2>Administrator Access</h2>
            <p>
              You can review documents and manage all three
              approval stages.
            </p>
          </div>
        )}

        {user?.role === "Viewer" && (
          <div className="workflow-information">
            <h2>Viewer Access</h2>
            <p>
              You have read-only access to the approval workflow.
            </p>
          </div>
        )}

        {/* ===================================================
            STATISTICS
        ==================================================== */}

        <section className="approval-statistics">

          <div className="approval-stat-card">

            <div className="approval-stat-icon">
              📄
            </div>

            <div>
              <h3>Total Documents</h3>

              <p>
                {loading ? "..." : documents.length}
              </p>
            </div>

          </div>

          <div className="approval-stat-card">

            <div className="approval-stat-icon">
              ⏳
            </div>

            <div>
              <h3>Pending</h3>

              <p>
                {loading ? "..." : pendingDocuments.length}
              </p>
            </div>

          </div>

          <div className="approval-stat-card">

            <div className="approval-stat-icon">
              ✅
            </div>

            <div>
              <h3>Approved</h3>

              <p>
                {loading ? "..." : approvedDocuments.length}
              </p>
            </div>

          </div>

          <div className="approval-stat-card">

            <div className="approval-stat-icon">
              ❌
            </div>

            <div>
              <h3>Rejected</h3>

              <p>
                {loading ? "..." : rejectedDocuments.length}
              </p>
            </div>

          </div>

        </section>

        {/* ===================================================
            THREE STAGE PROCESS
        ==================================================== */}

        <section className="workflow-information">

          <h2>Three-Stage Approval Process</h2>

          <div className="workflow-stages">

            <div className="workflow-stage">

              <div className="stage-number">
                1
              </div>

              <div>
                <h3>Reviewer</h3>
                <p>Initial document review</p>
              </div>

            </div>

            <div className="workflow-arrow">
              →
            </div>

            <div className="workflow-stage">

              <div className="stage-number">
                2
              </div>

              <div>
                <h3>Manager</h3>
                <p>Management approval</p>
              </div>

            </div>

            <div className="workflow-arrow">
              →
            </div>

            <div className="workflow-stage">

              <div className="stage-number">
                3
              </div>

              <div>
                <h3>Finance</h3>
                <p>Final approval</p>
              </div>

            </div>

          </div>

        </section>

        {/* ===================================================
            ERROR
        ==================================================== */}

        {errorMessage && !selectedDocument && (
          <div className="workflow-error">
            {errorMessage}
          </div>
        )}

        {/* ===================================================
            DOCUMENTS
        ==================================================== */}

        <section className="approval-documents">

          <div className="section-heading">

            <div>
              <h2>Documents</h2>

              <p>
                Track each document through the approval stages.
              </p>
            </div>

            <button
              className="refresh-button"
              onClick={loadWorkflow}
              disabled={loading}
            >
              🔄 Refresh
            </button>

          </div>

          {loading ? (

            <div className="workflow-empty">

              <div className="empty-icon">
                ⏳
              </div>

              <h3>Loading workflow...</h3>

              <p>
                Retrieving documents and approval stages.
              </p>

            </div>

          ) : documents.length === 0 ? (

            <div className="workflow-empty">

              <div className="empty-icon">
                📄
              </div>

              <h3>No documents found</h3>

              <p>
                Upload an invoice or credit note to begin the
                approval workflow.
              </p>

              <button
                className="primary-button"
                onClick={() => navigate("/documents")}
              >
                Go to Documents
              </button>

            </div>

          ) : (

            <div className="approval-document-list">

              {documents.map((document) => {

                const currentStage = getCurrentStage(
                  document.id
                );

                const stage1Status = getStageStatus(
                  document.id,
                  1
                );

                const stage2Status = getStageStatus(
                  document.id,
                  2
                );

                const stage3Status = getStageStatus(
                  document.id,
                  3
                );

                const canReview = canApproveStage(
                  document.id,
                  currentStage
                );

                return (
                  <div
                    className="approval-document-card"
                    key={document.id}
                  >

                    {/* Document Header */}

                    <div className="document-card-header">

                      <div>

                        <h3>
                          {document.fileName}
                        </h3>

                        <p>
                          Document ID: #{document.id}
                        </p>

                      </div>

                      <span
                        className={`status-badge ${getDocumentStatusClass(
                          document.status
                        )}`}
                      >
                        {document.status || "Pending"}
                      </span>

                    </div>

                    {/* Document Information */}

                    <div className="document-details">

                      <div>
                        <span>Vendor</span>

                        <strong>
                          {document.invoiceData?.vendor ||
                            document.vendor ||
                            "Not extracted"}
                        </strong>
                      </div>

                      <div>
                        <span>Invoice Number</span>

                        <strong>
                          {document.invoiceData?.invoiceNumber ||
                            document.invoiceNumber ||
                            "Not available"}
                        </strong>
                      </div>

                      <div>
                        <span>Total Amount</span>

                        <strong>
                          {document.invoiceData?.totalAmount != null
                            ? `R ${Number(
                                document.invoiceData.totalAmount
                              ).toLocaleString("en-ZA", {
                                minimumFractionDigits: 2,
                                maximumFractionDigits: 2
                              })}`
                            : document.totalAmount != null
                              ? `R ${Number(
                                  document.totalAmount
                                ).toLocaleString("en-ZA", {
                                  minimumFractionDigits: 2,
                                  maximumFractionDigits: 2
                                })}`
                              : "Not available"}
                        </strong>
                      </div>

                    </div>

                    {/* Approval Stages */}

                    <div className="approval-stages">

                      {/* Stage 1 */}

                      <div
                        className={`approval-stage ${
                          currentStage === 1
                            ? "current-stage"
                            : ""
                        }`}
                      >

                        <div className="stage-top">

                          <div className="stage-circle">
                            {stage1Status === "Approved"
                              ? "✓"
                              : stage1Status === "Rejected"
                                ? "✕"
                                : "1"}
                          </div>

                          <div>
                            <h4>Reviewer</h4>

                            <span>
                              Stage 1
                            </span>
                          </div>

                        </div>

                        <span
                          className={`stage-status ${getStatusClass(
                            stage1Status
                          )}`}
                        >
                          {stage1Status}
                        </span>

                      </div>

                      <div className="stage-connector">
                        →
                      </div>

                      {/* Stage 2 */}

                      <div
                        className={`approval-stage ${
                          currentStage === 2
                            ? "current-stage"
                            : ""
                        }`}
                      >

                        <div className="stage-top">

                          <div className="stage-circle">
                            {stage2Status === "Approved"
                              ? "✓"
                              : stage2Status === "Rejected"
                                ? "✕"
                                : "2"}
                          </div>

                          <div>
                            <h4>Manager</h4>

                            <span>
                              Stage 2
                            </span>
                          </div>

                        </div>

                        <span
                          className={`stage-status ${getStatusClass(
                            stage2Status
                          )}`}
                        >
                          {stage2Status}
                        </span>

                      </div>

                      <div className="stage-connector">
                        →
                      </div>

                      {/* Stage 3 */}

                      <div
                        className={`approval-stage ${
                          currentStage === 3
                            ? "current-stage"
                            : ""
                        }`}
                      >

                        <div className="stage-top">

                          <div className="stage-circle">
                            {stage3Status === "Approved"
                              ? "✓"
                              : stage3Status === "Rejected"
                                ? "✕"
                                : "3"}
                          </div>

                          <div>
                            <h4>Finance</h4>

                            <span>
                              Stage 3
                            </span>
                          </div>

                        </div>

                        <span
                          className={`stage-status ${getStatusClass(
                            stage3Status
                          )}`}
                        >
                          {stage3Status}
                        </span>

                      </div>

                    </div>

                    {/* =================================================
                        ACTION BUTTON
                    ================================================== */}

                    <div className="document-card-footer">

                      {canReview ? (

                        <button
                          className="approve-document-button"
                          onClick={() =>
                            handleOpenApproval(document)
                          }
                        >
                          Review Document
                        </button>

                      ) : (

                        <span className="workflow-message">

                          {document.status === "Approved"
                            ? "✓ Fully approved"
                            : document.status === "Rejected"
                              ? "✕ Workflow rejected"
                              : currentStage > 0
                                ? `Waiting for ${getRoleForStage(
                                    currentStage
                                  )}`
                                : "Workflow complete"}

                        </span>

                      )}

                    </div>

                  </div>
                );
              })}

            </div>

          )}

        </section>

      </main>

      {/* =====================================================
          APPROVAL MODAL
      ====================================================== */}

      {selectedDocument && (

        <div className="approval-modal-overlay">

          <div className="approval-modal">

            <div className="modal-header">

              <div>

                <h2>
                  Review Document
                </h2>

                <p>
                  {selectedDocument.fileName}
                </p>

              </div>

              <button
                className="modal-close"
                onClick={handleCloseApproval}
                disabled={actionLoading}
              >
                ×
              </button>

            </div>

            <div className="modal-body">

              {/* Document Information */}

              <div className="modal-document-info">

                <div>
                  <span>Document ID</span>

                  <strong>
                    #{selectedDocument.id}
                  </strong>
                </div>

                <div>
                  <span>Vendor</span>

                  <strong>
                    {selectedDocument.invoiceData?.vendor ||
                      selectedDocument.vendor ||
                      "Not extracted"}
                  </strong>
                </div>

                <div>
                  <span>Invoice Number</span>

                  <strong>
                    {selectedDocument.invoiceData?.invoiceNumber ||
                      selectedDocument.invoiceNumber ||
                      "Not available"}
                  </strong>
                </div>

              </div>

              {/* Current Stage */}

              <div className="modal-current-stage">

                <span>
                  Current approval stage
                </span>

                <strong>
                  {getRoleForStage(
                    getCurrentStage(selectedDocument.id)
                  )}
                </strong>

              </div>

              {/* Comments */}

              <div className="comments-section">

                <label htmlFor="approval-comments">
                  Comments
                </label>

                <textarea
                  id="approval-comments"
                  value={comments}
                  onChange={(event) =>
                    setComments(event.target.value)
                  }
                  placeholder="Enter comments about this approval decision..."
                  rows="5"
                  disabled={actionLoading}
                />

              </div>

              {/* Success */}

              {message && (
                <div className="workflow-success">
                  {message}
                </div>
              )}

              {/* Error */}

              {errorMessage && (
                <div className="workflow-error">
                  {errorMessage}
                </div>
              )}

            </div>

            {/* Modal Buttons */}

            <div className="modal-footer">

              <button
                className="reject-button"
                onClick={() =>
                  handleApprovalAction("Rejected")
                }
                disabled={actionLoading}
              >
                {actionLoading
                  ? "Processing..."
                  : "✕ Reject"}
              </button>

              <button
                className="approve-button"
                onClick={() =>
                  handleApprovalAction("Approved")
                }
                disabled={actionLoading}
              >
                {actionLoading
                  ? "Processing..."
                  : "✓ Approve"}
              </button>

            </div>

          </div>

        </div>

      )}

    </div>
  );
}

export default ApprovalWorkflow;

