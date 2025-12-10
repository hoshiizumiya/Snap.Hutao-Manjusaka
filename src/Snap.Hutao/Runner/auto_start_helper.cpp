#include "pch.h"
#include "auto_start_helper.h"

#include <Windows.h>
#include <Lmcons.h>
#include <string>
#include <comdef.h>
#include <taskschd.h>

// Linker libs required for Task Scheduler COM GUIDs and OLE functions
#pragma comment(lib, "Taskschd.lib")
#pragma comment(lib, "Ole32.lib")

// Simplified helper macros (no external logger dependency)
#define ExitOnFailure(hrExpr, msg) \
    if (FAILED(hrExpr))            \
    {                              \
        OutputDebugStringA(msg);   \
        goto LExit;                \
    }

#define ExitWithLastError(hrExpr, msg)            \
    {                                             \
        DWORD util_err = ::GetLastError();        \
        hrExpr = HRESULT_FROM_WIN32(util_err);    \
        OutputDebugStringA(msg);                  \
        goto LExit;                               \
    }

#define ExitFunction() \
    {                  \
        goto LExit;    \
    }

const DWORD USERNAME_DOMAIN_LEN = DNLEN + UNLEN + 2; // Domain Name + '\\' + User Name + '\0'
const DWORD USERNAME_LEN = UNLEN + 1; // User Name + '\0'

static bool internal_create_auto_start_task_for_this_user(bool runElevated)
{
    HRESULT hr = S_OK;

    WCHAR username_domain[USERNAME_DOMAIN_LEN];
    WCHAR username[USERNAME_LEN];

    std::wstring wstrTaskName;

    ITaskService* pService = NULL;
    ITaskFolder* pTaskFolder = NULL;
    ITaskDefinition* pTask = NULL;
    IRegistrationInfo* pRegInfo = NULL;
    ITaskSettings* pSettings = NULL;
    ITriggerCollection* pTriggerCollection = NULL;
    IRegisteredTask* pRegisteredTask = NULL;

    // ------------------------------------------------------
    // Get the Domain/Username for the trigger.
    if (!GetEnvironmentVariableW(L"USERNAME", username, USERNAME_LEN))
    {
        ExitWithLastError(hr, "Getting username failed\n");
    }
    if (!GetEnvironmentVariableW(L"USERDOMAIN", username_domain, USERNAME_DOMAIN_LEN))
    {
        ExitWithLastError(hr, "Getting the user's domain failed\n");
    }
    wcscat_s(username_domain, USERNAME_DOMAIN_LEN, L"\\");
    wcscat_s(username_domain, USERNAME_DOMAIN_LEN, username);

    // Task Name.
    wstrTaskName = L"Autorun for ";
    wstrTaskName += username;

    // Get the executable path passed to the custom action.
    WCHAR wszExecutablePath[MAX_PATH];
    GetModuleFileNameW(NULL, wszExecutablePath, MAX_PATH);

    // ------------------------------------------------------
    // Create an instance of the Task Service.
    hr = CoCreateInstance(CLSID_TaskScheduler,
        NULL,
        CLSCTX_INPROC_SERVER,
        IID_ITaskService,
        reinterpret_cast<void**>(&pService));
    ExitOnFailure(hr, "Failed to create an instance of ITaskService\n");

    // Connect to the task service.
    hr = pService->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t());
    ExitOnFailure(hr, "ITaskService::Connect failed\n");

    // ------------------------------------------------------
    // Get the Hutao task folder. Creates it if it doesn't exist.
    hr = pService->GetFolder(_bstr_t(L"\\Hutao"), &pTaskFolder);
    if (FAILED(hr))
    {
        // Folder doesn't exist. Get the Root folder and create the Hutao subfolder.
        ITaskFolder* pRootFolder = NULL;
        hr = pService->GetFolder(_bstr_t(L"\\"), &pRootFolder);
        ExitOnFailure(hr, "Cannot get Root Folder pointer\n");
        hr = pRootFolder->CreateFolder(_bstr_t(L"\\Hutao"), _variant_t(L""), &pTaskFolder);
        if (FAILED(hr))
        {
            pRootFolder->Release();
            ExitOnFailure(hr, "Cannot create Hutao task folder\n");
        }
    }

    // If the task exists, just enable it.
    {
        IRegisteredTask* pExistingRegisteredTask = NULL;
        hr = pTaskFolder->GetTask(_bstr_t(wstrTaskName.c_str()), &pExistingRegisteredTask);
        if (SUCCEEDED(hr))
        {
            // Task exists, try enabling it.
            hr = pExistingRegisteredTask->put_Enabled(VARIANT_TRUE);
            pExistingRegisteredTask->Release();
            if (SUCCEEDED(hr))
            {
                // Function enable. Sounds like a success.
                ExitFunction();
            }
        }
    }

    // Create the task builder object to create the task.
    hr = pService->NewTask(0, &pTask);
    ExitOnFailure(hr, "Failed to create a task definition\n");

    // ------------------------------------------------------
    // Get the registration info for setting the identification.
    hr = pTask->get_RegistrationInfo(&pRegInfo);
    ExitOnFailure(hr, "Cannot get identification pointer\n");
    hr = pRegInfo->put_Author(_bstr_t(username_domain));
    ExitOnFailure(hr, "Cannot put identification info\n");

    // ------------------------------------------------------
    // Create the settings for the task
    hr = pTask->get_Settings(&pSettings);
    ExitOnFailure(hr, "Cannot get settings pointer\n");

    hr = pSettings->put_StartWhenAvailable(VARIANT_FALSE);
    ExitOnFailure(hr, "Cannot put_StartWhenAvailable setting info\n");
    hr = pSettings->put_StopIfGoingOnBatteries(VARIANT_FALSE);
    ExitOnFailure(hr, "Cannot put_StopIfGoingOnBatteries setting info\n");
    hr = pSettings->put_ExecutionTimeLimit(_bstr_t(L"PT0S")); //Unlimited
    ExitOnFailure(hr, "Cannot put_ExecutionTimeLimit setting info\n");
    hr = pSettings->put_DisallowStartIfOnBatteries(VARIANT_FALSE);
    ExitOnFailure(hr, "Cannot put_DisallowStartIfOnBatteries setting info\n");
    hr = pSettings->put_Priority(4);
    ExitOnFailure(hr, "Cannot put_Priority setting info\n");

    // ------------------------------------------------------
    // Get the trigger collection to insert the logon trigger.
    hr = pTask->get_Triggers(&pTriggerCollection);
    ExitOnFailure(hr, "Cannot get trigger collection\n");

    // Add the logon trigger to the task.
    {
        ITrigger* pTrigger = NULL;
        ILogonTrigger* pLogonTrigger = NULL;
        hr = pTriggerCollection->Create(TASK_TRIGGER_LOGON, &pTrigger);
        ExitOnFailure(hr, "Cannot create the trigger\n");

        hr = pTrigger->QueryInterface(
            IID_ILogonTrigger, reinterpret_cast<void**>(&pLogonTrigger));
        pTrigger->Release();
        ExitOnFailure(hr, "QueryInterface call failed for ILogonTrigger\n");

        hr = pLogonTrigger->put_Id(_bstr_t(L"Trigger1"));

        // Timing issues may make explorer not be started when the task runs.
        // Add a little delay to mitigate this.
        hr = pLogonTrigger->put_Delay(_bstr_t(L"PT03S"));

        // Define the user. The task will execute when the user logs on.
        // The specified user must be a user on this computer.
        hr = pLogonTrigger->put_UserId(_bstr_t(username_domain));
        pLogonTrigger->Release();
        ExitOnFailure(hr, "Cannot add user ID to logon trigger\n");
    }

    // ------------------------------------------------------
    // Add an Action to the task. This task will execute the path passed to this custom action.
    {
        IActionCollection* pActionCollection = NULL;
        IAction* pAction = NULL;
        IExecAction* pExecAction = NULL;

        // Get the task action collection pointer.
        hr = pTask->get_Actions(&pActionCollection);
        ExitOnFailure(hr, "Cannot get Task collection pointer\n");

        // Create the action, specifying that it is an executable action.
        hr = pActionCollection->Create(TASK_ACTION_EXEC, &pAction);
        pActionCollection->Release();
        ExitOnFailure(hr, "Cannot create the action\n");

        // QI for the executable task pointer.
        hr = pAction->QueryInterface(
            IID_IExecAction, reinterpret_cast<void**>(&pExecAction));
        pAction->Release();
        ExitOnFailure(hr, "QueryInterface call failed for IExecAction\n");

        // Set the path of the executable to Hutao (passed as CustomActionData).
        hr = pExecAction->put_Path(_bstr_t(wszExecutablePath));
        pExecAction->Release();
        ExitOnFailure(hr, "Cannot set path of executable\n");
    }

    // ------------------------------------------------------
    // Create the principal for the task
    {
        IPrincipal* pPrincipal = NULL;
        hr = pTask->get_Principal(&pPrincipal);
        ExitOnFailure(hr, "Cannot get principal pointer\n");

        // Set up principal information:
        hr = pPrincipal->put_Id(_bstr_t(L"Principal1"));

        hr = pPrincipal->put_UserId(_bstr_t(username_domain));

        hr = pPrincipal->put_LogonType(TASK_LOGON_INTERACTIVE_TOKEN);

        if (runElevated)
        {
            hr = pPrincipal->put_RunLevel(_TASK_RUNLEVEL::TASK_RUNLEVEL_HIGHEST);
        }
        else
        {
            hr = pPrincipal->put_RunLevel(_TASK_RUNLEVEL::TASK_RUNLEVEL_LUA);
        }
        pPrincipal->Release();
        ExitOnFailure(hr, "Cannot put principal run level\n");
    }
    // ------------------------------------------------------
    //  Save the task in the Hutao folder.
    {
        _variant_t SDDL_FULL_ACCESS_FOR_EVERYONE = L"D:(A;;FA;;;WD)";
        hr = pTaskFolder->RegisterTaskDefinition(
            _bstr_t(wstrTaskName.c_str()),
            pTask,
            TASK_CREATE_OR_UPDATE,
            _variant_t(username_domain),
            _variant_t(),
            TASK_LOGON_INTERACTIVE_TOKEN,
            SDDL_FULL_ACCESS_FOR_EVERYONE,
            &pRegisteredTask);
        ExitOnFailure(hr, "Error saving the Task\n");
    }

LExit:
    if (pService)
        pService->Release();
    if (pTaskFolder)
        pTaskFolder->Release();
    if (pTask)
        pTask->Release();
    if (pRegInfo)
        pRegInfo->Release();
    if (pSettings)
        pSettings->Release();
    if (pTriggerCollection)
        pTriggerCollection->Release();
    if (pRegisteredTask)
        pRegisteredTask->Release();

    return (SUCCEEDED(hr));
}

static bool internal_delete_auto_start_task_for_this_user()
{
    HRESULT hr = S_OK;

    WCHAR username[USERNAME_LEN];
    std::wstring wstrTaskName;

    ITaskService* pService = NULL;
    ITaskFolder* pTaskFolder = NULL;

    // ------------------------------------------------------
    // Get the Username for the task.
    if (!GetEnvironmentVariableW(L"USERNAME", username, USERNAME_LEN))
    {
        ExitWithLastError(hr, "Getting username failed\n");
    }

    // Task Name.
    wstrTaskName = L"Autorun for ";
    wstrTaskName += username;

    // ------------------------------------------------------
    // Create an instance of the Task Service.
    hr = CoCreateInstance(CLSID_TaskScheduler,
        NULL,
        CLSCTX_INPROC_SERVER,
        IID_ITaskService,
        reinterpret_cast<void**>(&pService));
    ExitOnFailure(hr, "Failed to create an instance of ITaskService\n");

    // Connect to the task service.
    hr = pService->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t());
    ExitOnFailure(hr, "ITaskService::Connect failed\n");

    // ------------------------------------------------------
    // Get the Hutao task folder.
    hr = pService->GetFolder(_bstr_t(L"\\Hutao"), &pTaskFolder);
    if (FAILED(hr))
    {
        // Folder doesn't exist. No need to disable a non-existing task.
        hr = S_OK;
        ExitFunction();
    }

    // ------------------------------------------------------
    // If the task exists, disable.
    {
        IRegisteredTask* pExistingRegisteredTask = NULL;
        hr = pTaskFolder->GetTask(_bstr_t(wstrTaskName.c_str()), &pExistingRegisteredTask);
        if (SUCCEEDED(hr))
        {
            // Task exists, try disabling it.
            hr = pTaskFolder->DeleteTask(_bstr_t(wstrTaskName.c_str()), 0);
        }
    }

LExit:
    if (pService)
        pService->Release();
    if (pTaskFolder)
        pTaskFolder->Release();

    return (SUCCEEDED(hr));
}

static bool internal_is_auto_start_task_active_for_this_user()
{
    HRESULT hr = S_OK;

    WCHAR username[USERNAME_LEN];
    std::wstring wstrTaskName;

    ITaskService* pService = NULL;
    ITaskFolder* pTaskFolder = NULL;

    // ------------------------------------------------------
    // Get the Username for the task.
    if (!GetEnvironmentVariableW(L"USERNAME", username, USERNAME_LEN))
    {
        ExitWithLastError(hr, "Getting username failed\n");
    }

    // Task Name.
    wstrTaskName = L"Autorun for ";
    wstrTaskName += username;

    // ------------------------------------------------------
    // Create an instance of the Task Service.
    hr = CoCreateInstance(CLSID_TaskScheduler,
        NULL,
        CLSCTX_INPROC_SERVER,
        IID_ITaskService,
        reinterpret_cast<void**>(&pService));
    ExitOnFailure(hr, "Failed to create an instance of ITaskService\n");

    // Connect to the task service.
    hr = pService->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t());
    ExitOnFailure(hr, "ITaskService::Connect failed\n");

    // ------------------------------------------------------
    // Get the Hutao task folder.
    hr = pService->GetFolder(_bstr_t(L"\\Hutao"), &pTaskFolder);
    ExitOnFailure(hr, "ITaskFolder doesn't exist\n");

    // ------------------------------------------------------
    // If the task exists, disable.
    {
        IRegisteredTask* pExistingRegisteredTask = NULL;
        hr = pTaskFolder->GetTask(_bstr_t(wstrTaskName.c_str()), &pExistingRegisteredTask);
        if (SUCCEEDED(hr))
        {
            // Task exists, get its value.
            VARIANT_BOOL is_enabled;
            hr = pExistingRegisteredTask->get_Enabled(&is_enabled);
            pExistingRegisteredTask->Release();
            if (SUCCEEDED(hr))
            {
                // Got the value. Return it.
                hr = (is_enabled == VARIANT_TRUE) ? S_OK : E_FAIL; // Fake success or fail to return the value.
                ExitFunction();
            }
        }
    }

LExit:
    if (pService)
        pService->Release();
    if (pTaskFolder)
        pTaskFolder->Release();

    return (SUCCEEDED(hr));
}

extern "C" __declspec(dllexport) BOOL WINAPI create_auto_start_task_for_this_user(BOOL runElevated)
{
    // Ensure COM initialized on this thread
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    bool result = false;
    if (SUCCEEDED(hr) || hr == RPC_E_CHANGED_MODE)
    {
        result = internal_create_auto_start_task_for_this_user(runElevated != FALSE);
        if (SUCCEEDED(hr))
        {
            CoUninitialize();
        }
    }
    return result ? TRUE : FALSE;
}

extern "C" __declspec(dllexport) BOOL WINAPI delete_auto_start_task_for_this_user()
{
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    bool result = false;
    if (SUCCEEDED(hr) || hr == RPC_E_CHANGED_MODE)
    {
        result = internal_delete_auto_start_task_for_this_user();
        if (SUCCEEDED(hr))
        {
            CoUninitialize();
        }
    }
    return result ? TRUE : FALSE;
}

extern "C" __declspec(dllexport) BOOL WINAPI is_auto_start_task_active_for_this_user()
{
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    bool result = false;
    if (SUCCEEDED(hr) || hr == RPC_E_CHANGED_MODE)
    {
        result = internal_is_auto_start_task_active_for_this_user();
        if (SUCCEEDED(hr))
        {
            CoUninitialize();
        }
    }
    return result ? TRUE : FALSE;
}
