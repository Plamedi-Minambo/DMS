using DocumentManagement.API.Data;
using DocumentManagement.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ApprovalsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApprovalsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ========================================
        // GET ALL APPROVALS FOR A DOCUMENT
        // ========================================

        [HttpGet("document/{documentId}")]
        [Authorize(Roles = "Admin,Reviewer,Manager,Finance,Viewer")]
        public async Task<IActionResult> GetDocumentApprovals(int documentId)
        {
            var documentExists = await _context.Documents
                .AnyAsync(d => d.Id == documentId);

            if (!documentExists)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            var approvals = await _context.Approvals
                .Where(a => a.DocumentId == documentId)
                .OrderBy(a => a.Stage)
                .ToListAsync();

            return Ok(approvals);
        }

        // ========================================
        // APPROVE DOCUMENT
        // ========================================

        [HttpPost("{documentId}/approve")]
        [Authorize(Roles = "Admin,Reviewer,Manager,Finance")]
        public async Task<IActionResult> ApproveDocument(
            int documentId,
            [FromBody] ApprovalActionRequest request)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "User could not be identified."
                });
            }

            var document = await _context.Documents
                .Include(d => d.InvoiceData)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            string role;

            // Admin can act as the appropriate approval authority
            if (userRoles.Contains("Admin"))
            {
                role = request.Role;
            }
            else
            {
                role = userRoles.FirstOrDefault(r =>
                    r == "Reviewer" ||
                    r == "Manager" ||
                    r == "Finance") ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                return Forbid();
            }

            if (role != "Reviewer" &&
                role != "Manager" &&
                role != "Finance")
            {
                return BadRequest(new
                {
                    message = "Invalid approval role."
                });
            }

            int stage = role switch
            {
                "Reviewer" => 1,
                "Manager" => 2,
                "Finance" => 3,
                _ => 0
            };

            // ========================================
            // CHECK PREVIOUS STAGES
            // ========================================

            if (stage > 1)
            {
                var previousStageApproved = await _context.Approvals
                    .AnyAsync(a =>
                        a.DocumentId == documentId &&
                        a.Stage == stage - 1 &&
                        a.Status == "Approved");

                if (!previousStageApproved)
                {
                    return BadRequest(new
                    {
                        message = $"Stage {stage - 1} must be approved before {role} can approve this document."
                    });
                }
            }

            // ========================================
            // CHECK CURRENT STAGE
            // ========================================

            var approval = await _context.Approvals
                .FirstOrDefaultAsync(a =>
                    a.DocumentId == documentId &&
                    a.Stage == stage);

            if (approval == null)
            {
                approval = new Approval
                {
                    DocumentId = documentId,
                    Stage = stage,
                    Role = role,
                    Status = "Pending"
                };

                _context.Approvals.Add(approval);
            }

            if (approval.Status == "Approved")
            {
                return BadRequest(new
                {
                    message = $"{role} has already approved this document."
                });
            }

            if (approval.Status == "Rejected")
            {
                return BadRequest(new
                {
                    message = "This approval stage has already been rejected."
                });
            }

            approval.Status = "Approved";
            approval.Comments = request.Comments;
            approval.ApprovedById = user.Id;
            approval.ActionDate = DateTime.UtcNow;

            // ========================================
            // UPDATE DOCUMENT STATUS
            // ========================================

            if (stage == 1)
            {
                document.Status = "Pending Manager";
            }
            else if (stage == 2)
            {
                document.Status = "Pending Finance";
            }
            else if (stage == 3)
            {
                document.Status = "Approved";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"{role} approval completed successfully.",
                documentId = documentId,
                stage = stage,
                role = role,
                status = document.Status
            });
        }

        // ========================================
        // REJECT DOCUMENT
        // ========================================

        [HttpPost("{documentId}/reject")]
        [Authorize(Roles = "Admin,Reviewer,Manager,Finance")]
        public async Task<IActionResult> RejectDocument(
            int documentId,
            [FromBody] ApprovalActionRequest request)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "User could not be identified."
                });
            }

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            string role;

            if (userRoles.Contains("Admin"))
            {
                role = request.Role;
            }
            else
            {
                role = userRoles.FirstOrDefault(r =>
                    r == "Reviewer" ||
                    r == "Manager" ||
                    r == "Finance") ?? string.Empty;
            }

            if (role != "Reviewer" &&
                role != "Manager" &&
                role != "Finance")
            {
                return Forbid();
            }

            int stage = role switch
            {
                "Reviewer" => 1,
                "Manager" => 2,
                "Finance" => 3,
                _ => 0
            };

            // ========================================
            // CHECK PREVIOUS STAGES
            // ========================================

            if (stage > 1)
            {
                var previousStageApproved = await _context.Approvals
                    .AnyAsync(a =>
                        a.DocumentId == documentId &&
                        a.Stage == stage - 1 &&
                        a.Status == "Approved");

                if (!previousStageApproved)
                {
                    return BadRequest(new
                    {
                        message = $"Stage {stage - 1} must be approved before {role} can reject this document."
                    });
                }
            }

            var approval = await _context.Approvals
                .FirstOrDefaultAsync(a =>
                    a.DocumentId == documentId &&
                    a.Stage == stage);

            if (approval == null)
            {
                approval = new Approval
                {
                    DocumentId = documentId,
                    Stage = stage,
                    Role = role
                };

                _context.Approvals.Add(approval);
            }

            approval.Status = "Rejected";
            approval.Comments = request.Comments;
            approval.ApprovedById = user.Id;
            approval.ActionDate = DateTime.UtcNow;

            document.Status = "Rejected";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"{role} rejected the document.",
                documentId = documentId,
                stage = stage,
                role = role,
                status = document.Status
            });
        }
    }

    // ========================================
    // APPROVAL REQUEST
    // ========================================

    public class ApprovalActionRequest
    {
        public string Role { get; set; } = string.Empty;

        public string? Comments { get; set; }
    }
}