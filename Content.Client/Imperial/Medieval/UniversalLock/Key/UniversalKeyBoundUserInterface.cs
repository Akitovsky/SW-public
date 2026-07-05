using Content.Shared.Imperial.Medieval.UniversalSecurity;

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

        // Изменено: лямбда-выражение теперь принимает и отправляет (name, code)
        _window.OnSetCode += (name, code) => SendMessage(new UniversalKeySetCodeMessage(code, name));
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
