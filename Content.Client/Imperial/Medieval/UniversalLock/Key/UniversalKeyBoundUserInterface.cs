using Content.Shared.Imperial.Medieval.Ships.Anchor;

namespace Imperial.Medieval.UniversalLock.Lock;

public sealed class UniversalKeyBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private UniversalKeyWindow? _window;

    public UniversalKeyBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = new UniversalKeyWindow();
        _window.OpenCentered();

        // Отправляем сообщение ковки ключа на сервер
        _window.OnSetCode += code => SendMessage(new UniversalKeySetCodeMessage(code));
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        // Проверяем именно состояние ключа
        if (state is not UniversalKeyBuiState)
            return;

        // Передаем максимальное доступное количество зубцов для заготовки (например, 10)
        _window?.UpdateState();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Close();
        _window?.Dispose();
    }
}
