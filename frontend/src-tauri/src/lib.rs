use tauri::Manager;
use tauri_plugin_shell::ShellExt;

/// Launches the .NET API as a background sidecar process.
/// The sidecar binary is resolved from `src-tauri/binaries/HarborTorrent.Api-<target-triple>`.
/// Per the Tauri sidecar docs: https://tauri.app/develop/sidecar/
fn start_api_sidecar(app: &tauri::AppHandle) {
    let shell = app.shell();

    match shell.sidecar("HarborTorrent.Api") {
        Ok(command) => {
            match command.spawn() {
                Ok((_rx, _child)) => {
                    // The sidecar is running. We intentionally drop _rx (stdout reader)
                    // and _child handle — Tauri will clean up the process when the app exits.
                    println!("[HarborTorrent] API sidecar started on http://localhost:5000");
                }
                Err(e) => {
                    eprintln!("[HarborTorrent] Failed to spawn API sidecar: {e}");
                }
            }
        }
        Err(e) => {
            eprintln!("[HarborTorrent] Failed to resolve API sidecar binary: {e}");
        }
    }
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .setup(|app| {
            // Start the .NET backend when the Tauri window opens.
            start_api_sidecar(&app.handle());
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running HarborTorrent");
}
