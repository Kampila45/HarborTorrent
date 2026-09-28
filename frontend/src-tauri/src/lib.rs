use rand::Rng;
use std::net::TcpListener;
use std::sync::Mutex;
use tauri::{
    menu::{Menu, MenuItem},
    tray::TrayIconBuilder,
    Manager, RunEvent, WindowEvent,
};
use tauri_plugin_shell::{process::CommandChild, ShellExt};

/// Holds the per-session authentication token and the port the API is listening on.
/// Both values are generated once at startup and never change.
struct AppSession {
    launch_token: String,
    api_port: u16,
}

/// Holds the sidecar child handle so it can be killed when the application exits.
struct SidecarHandle(Mutex<Option<CommandChild>>);

/// Selects a free TCP port on the loopback interface by binding to port 0 and
/// reading the address assigned by the OS. Falls back to 5000 if selection fails.
fn select_free_port() -> u16 {
    TcpListener::bind("127.0.0.1:0")
        .ok()
        .and_then(|listener| listener.local_addr().ok())
        .map(|addr| addr.port())
        .unwrap_or(5000)
}

/// Generates a 32-byte cryptographically random token encoded as a hex string.
fn generate_launch_token() -> String {
    let bytes: [u8; 32] = rand::thread_rng().gen();
    bytes.iter().map(|b| format!("{b:02x}")).collect()
}

/// Launches the .NET API sidecar with the launch token and port injected via
/// environment variables. Returns the child handle for lifecycle management.
/// The sidecar binary is resolved from `src-tauri/binaries/HarborTorrent.Api-<target-triple>`.
fn start_api_sidecar(
    app: &tauri::AppHandle,
    launch_token: &str,
    api_port: u16,
) -> Option<CommandChild> {
    let shell = app.shell();

    match shell.sidecar("HarborTorrent.Api") {
        Ok(command) => {
            let result = command
                .env("HARBOR_LAUNCH_TOKEN", launch_token)
                .env("HARBOR_API_PORT", api_port.to_string())
                .spawn();

            match result {
                Ok((mut rx, child)) => {
                    println!(
                        "[HarborTorrent] API sidecar started on http://127.0.0.1:{api_port}"
                    );

                    // Drain the receiver to keep the stdout/stderr pipes open.
                    // The launch token is intentionally not logged here.
                    tauri::async_runtime::spawn(async move {
                        while let Some(event) = rx.recv().await {
                            match event {
                                tauri_plugin_shell::process::CommandEvent::Stdout(line) => {
                                    print!(
                                        "[HarborTorrent.Api] {}",
                                        String::from_utf8_lossy(&line)
                                    );
                                }
                                tauri_plugin_shell::process::CommandEvent::Stderr(line) => {
                                    eprint!(
                                        "[HarborTorrent.Api] {}",
                                        String::from_utf8_lossy(&line)
                                    );
                                }
                                _ => {}
                            }
                        }
                    });

                    Some(child)
                }
                Err(e) => {
                    eprintln!("[HarborTorrent] Failed to spawn API sidecar: {e}");
                    None
                }
            }
        }
        Err(e) => {
            eprintln!("[HarborTorrent] Failed to resolve API sidecar binary: {e}");
            None
        }
    }
}

/// Returns the per-session launch token to the WebView.
/// The token is used by the frontend HTTP client to authenticate every request.
#[tauri::command]
fn get_launch_token(session: tauri::State<AppSession>) -> String {
    session.launch_token.clone()
}

/// Returns the port the API sidecar is listening on.
/// The frontend uses this to construct the base URL dynamically.
#[tauri::command]
fn get_api_port(session: tauri::State<AppSession>) -> u16 {
    session.api_port
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let launch_token = generate_launch_token();
    let api_port = select_free_port();

    let session = AppSession {
        launch_token: launch_token.clone(),
        api_port,
    };

    tauri::Builder::default()
        .manage(session)
        .manage(SidecarHandle(Mutex::new(None)))
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_process::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .plugin(tauri_plugin_notification::init())
        .plugin(tauri_plugin_fs::init())
        .invoke_handler(tauri::generate_handler![get_launch_token, get_api_port])
        .setup(move |app| {
            // Start the .NET backend and store the child handle for lifecycle management.
            let child = start_api_sidecar(&app.handle(), &launch_token, api_port);
            if let Some(child) = child {
                let handle_state = app.state::<SidecarHandle>();
                *handle_state.0.lock().unwrap() = Some(child);
            }

            // Setup System Tray menu.
            let quit_i = MenuItem::with_id(app, "quit", "Quit", true, None::<&str>)?;
            let show_i = MenuItem::with_id(app, "show", "Show", true, None::<&str>)?;
            let menu = Menu::with_items(app, &[&show_i, &quit_i])?;

            let _tray = TrayIconBuilder::new()
                .icon(app.default_window_icon().unwrap().clone())
                .menu(&menu)
                .on_menu_event(|app, event| match event.id.as_ref() {
                    "quit" => {
                        app.exit(0);
                    }
                    "show" => {
                        if let Some(window) = app.get_webview_window("main") {
                            let _ = window.show();
                            let _ = window.set_focus();
                        }
                    }
                    _ => {}
                })
                .on_tray_icon_event(|tray, event| {
                    if let tauri::tray::TrayIconEvent::Click {
                        button: tauri::tray::MouseButton::Left,
                        button_state: tauri::tray::MouseButtonState::Up,
                        ..
                    } = event
                    {
                        let app = tray.app_handle();
                        if let Some(window) = app.get_webview_window("main") {
                            let _ = window.show();
                            let _ = window.set_focus();
                        }
                    }
                })
                .build(app)?;

            Ok(())
        })
        .on_window_event(|window, event| {
            // Closing the window minimizes to tray rather than quitting.
            if let WindowEvent::CloseRequested { api, .. } = event {
                window.hide().unwrap();
                api.prevent_close();
            }
        })
        .build(tauri::generate_context!())
        .expect("error while building HarborTorrent")
        .run(|app, event| {
            // Kill the sidecar when the application exits so no orphaned process remains.
            if let RunEvent::Exit = event {
                let handle_state = app.state::<SidecarHandle>();
                if let Some(child) = handle_state.0.lock().unwrap().take() {
                    let _ = child.kill();
                    println!("[HarborTorrent] API sidecar stopped.");
                }
            }
        });
}
