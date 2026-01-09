#pragma once

#include <Windows.h>

#ifdef __cplusplus
extern "C" {
#endif

__declspec(dllexport) BOOL WINAPI is_auto_start_task_active_for_this_user();
__declspec(dllexport) BOOL WINAPI create_auto_start_task_for_this_user(BOOL runElevated);
__declspec(dllexport) BOOL WINAPI delete_auto_start_task_for_this_user();

// Returns TRUE if task exists and runs with highest privileges, otherwise FALSE.
__declspec(dllexport) BOOL WINAPI is_auto_start_task_run_elevated_for_this_user();

// Writes the task executable path into buffer (including null terminator).
// Returns TRUE on success.
__declspec(dllexport) BOOL WINAPI get_auto_start_task_executable_path_for_this_user(_Out_writes_z_(cchBuffer) WCHAR* buffer, DWORD cchBuffer);

#ifdef __cplusplus
}
#endif
