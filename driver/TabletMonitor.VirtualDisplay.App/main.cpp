// Software-device host for the TabletMonitor indirect display driver.
// Based on the Microsoft Windows Driver Samples IndirectDisplay application.

#include <cstdio>
#include <windows.h>
#include <swdevice.h>

namespace
{
    HANDLE g_StopEvent = nullptr;

    struct CreationContext
    {
        HANDLE Event;
        HRESULT Result;
    };

    VOID WINAPI CreationCallback(
        _In_ HSWDEVICE hSwDevice,
        _In_ HRESULT hrCreateResult,
        _In_opt_ PVOID pContext,
        _In_opt_ PCWSTR pszDeviceInstanceId)
    {
        auto* context = static_cast<CreationContext*>(pContext);
        context->Result = hrCreateResult;
        SetEvent(context->Event);

        UNREFERENCED_PARAMETER(hSwDevice);
        UNREFERENCED_PARAMETER(pszDeviceInstanceId);
    }

    BOOL WINAPI ConsoleHandler(DWORD signal)
    {
        if (signal == CTRL_C_EVENT || signal == CTRL_BREAK_EVENT ||
            signal == CTRL_CLOSE_EVENT || signal == CTRL_SHUTDOWN_EVENT)
        {
            SetEvent(g_StopEvent);
            return TRUE;
        }

        return FALSE;
    }
}

int __cdecl wmain()
{
    constexpr PCWSTR DeviceId = L"TabletMonitorVirtualDisplay";
    constexpr PCWSTR DeviceDescription = L"TabletMonitor Virtual Display";
    constexpr PCWSTR MultiStringIds = L"TabletMonitorVirtualDisplay\0\0";

    CreationContext context = {};
    context.Event = CreateEvent(nullptr, FALSE, FALSE, nullptr);
    context.Result = E_PENDING;
    g_StopEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);

    if (context.Event == nullptr || g_StopEvent == nullptr)
    {
        std::printf("Nao foi possivel criar os eventos do aplicativo.\n");
        return 1;
    }

    SetConsoleCtrlHandler(ConsoleHandler, TRUE);

    SW_DEVICE_CREATE_INFO createInfo = {};
    createInfo.cbSize = sizeof(createInfo);
    createInfo.pszzCompatibleIds = MultiStringIds;
    createInfo.pszInstanceId = DeviceId;
    createInfo.pszzHardwareIds = MultiStringIds;
    createInfo.pszDeviceDescription = DeviceDescription;
    createInfo.CapabilityFlags =
        SWDeviceCapabilitiesRemovable |
        SWDeviceCapabilitiesSilentInstall |
        SWDeviceCapabilitiesDriverRequired;

    HSWDEVICE softwareDevice = nullptr;
    HRESULT result = SwDeviceCreate(
        DeviceId,
        L"HTREE\\ROOT\\0",
        &createInfo,
        0,
        nullptr,
        CreationCallback,
        &context,
        &softwareDevice);

    if (FAILED(result))
    {
        std::printf("SwDeviceCreate falhou: 0x%08lX\n", result);
        CloseHandle(context.Event);
        CloseHandle(g_StopEvent);
        return 1;
    }

    DWORD waitResult = WaitForSingleObject(context.Event, 15000);
    if (waitResult != WAIT_OBJECT_0 || FAILED(context.Result))
    {
        std::printf("O monitor virtual nao foi criado. Resultado: 0x%08lX\n", context.Result);
        SwDeviceClose(softwareDevice);
        CloseHandle(context.Event);
        CloseHandle(g_StopEvent);
        return 1;
    }

    std::printf("TabletMonitor Virtual Display conectado.\n");
    std::printf("Mantenha esta janela aberta. Pressione Ctrl+C para desconectar.\n");

    WaitForSingleObject(g_StopEvent, INFINITE);

    std::printf("\nDesconectando o monitor virtual...\n");
    SwDeviceClose(softwareDevice);
    CloseHandle(context.Event);
    CloseHandle(g_StopEvent);
    return 0;
}
