using System.Windows;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class MainWindow
{
    private void OpenZaloSchedules_Click(object sender, RoutedEventArgs e) =>
        RequestRootNavigation(OpenZaloSchedules);

    private void OpenZaloSchedules()
    {
        if (_state is null || !CanOpenAnotherPage()) return;
        ZaloScheduleWindow? page = null;
        page = new ZaloScheduleWindow(State)
        {
            PersistPlansAsync = () => PersistAsync(false),
            OpenDraftEditor = planned =>
            {
                var editor = new MovementWindow(State, editing: planned.Draft);
                ShowEmbeddedPage(editor, "Xem/sửa phiếu chờ từ lịch Zalo",
                    "Phiếu vẫn là dự thảo; không nhập hoặc xuất thực tế cho tới khi được xác nhận.",
                    accepted =>
                    {
                        if (!accepted || editor.Draft is not MovementDraft reviewed) return;
                        _ = SaveZaloPendingAsync(reviewed, planned.PlanIds, page!);
                    }, isChildOfCurrentPage: true);
            }
        };
        ShowEmbeddedPage(page, "Lịch Zalo · Tổng hợp hợp đồng",
            "Chọn hợp đồng trong ảnh và lưu kế hoạch trước khi lập phiếu nhập/xuất.");
    }

    private async Task SaveZaloPendingAsync(MovementDraft reviewed,
        IReadOnlyList<string> planIds, ZaloScheduleWindow page)
    {
        // Even if a user changes form controls, this route can ONLY stage a
        // pending document. Actual movements require a separate confirmation.
        if (!reviewed.Pending)
        {
            ShowToast("Phiếu Zalo chỉ được lập ở trạng thái chờ xác nhận.");
            return;
        }
        if (planIds.Count == 0 || planIds.Count != planIds.Distinct().Count())
        {
            ShowToast("Danh sách kế hoạch chờ lập phiếu không hợp lệ.");
            return;
        }
        if (State.ZaloPlans.Count(p => planIds.Contains(p.Id)) != planIds.Count ||
            State.ZaloPlans.Any(p => planIds.Contains(p.Id) && p.PendingDocNo.Length > 0))
        {
            ShowToast("Hợp đồng đã có phiếu chờ hoặc dữ liệu kế hoạch vừa thay đổi. Hãy mở lại danh sách.");
            return;
        }
        var docNo = NextMovementDocNo(reviewed.Type, reviewed.Date);
        var pending = ToPendingMovement(reviewed, docNo);
        State.PendingMovements.Add(pending);
        try
        {
            ZaloScheduleService.MarkPending(State.ZaloPlans, planIds, docNo);
            if (!await PersistAsync(false))
                throw new IOException("Không lưu được phiếu chờ cùng trạng thái kế hoạch.");
            page.RefreshPlans();
            ShowToast($"Đã tạo {docNo} ở trạng thái chờ xác nhận. Chưa ghi Excel hay trừ tồn.");
        }
        catch (Exception ex)
        {
            State.PendingMovements.Remove(pending);
            foreach (var plan in State.ZaloPlans.Where(p => planIds.Contains(p.Id) && p.PendingDocNo == docNo))
                plan.PendingDocNo = "";
            page.RefreshPlans();
            ShowToast("Không tạo được phiếu chờ: " + ex.Message);
        }
    }
}
