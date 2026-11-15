use rand::Rng;
use std::collections::VecDeque;
use std::io::{Read, Write};
use std::net::{SocketAddr, TcpListener, TcpStream};
use std::sync::{Arc, Mutex};
use std::time::Duration;
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

/// Captures the most recent sidecar stdout/stderr lines for troubleshooting startup errors.
struct SidecarLogs(Arc<Mutex<VecDeque<String>>>);

const MAX_SIDECAR_LOG_LINES: usize = 60;

fn push_sidecar_log(logs: &Arc<Mutex<VecDeque<String>>>, line: String) {
    if let Ok(mut guard) = logs.lock() {
        if guard.len() >= MAX_SIDECAR_LOG_LINES {
            guard.pop_front();
        }
        guard.push_back(line);
    }
}

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

/// Requests a graceful backend shutdown so the torrent engine can stop active
/// transfers and flush fast-resume/engine state before the process is terminated.
fn request_graceful_shutdown(api_port: u16, launch_token: &str) {
    let addr: SocketAddr = ([127, 0, 0, 1], api_port).into();
    if let Ok(mut stream) = TcpStream::connect_timeout(&addr, Duration::from_millis(500)) {
        let _ = stream.set_read_timeout(Some(Duration::from_millis(1500)));
        let _ = stream.set_write_timeout(Some(Duration::from_millis(500)));
        let request = format!(
            "POST /api/v1/system/shutdown HTTP/1.1\r\nHost: 127.0.0.1:{api_port}\r\nX-Launch-Token: {launch_token}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
        );
        if stream.write_all(request.as_bytes()).is_ok() {
            let mut buf = [0u8; 256];
            let _ = stream.read(&mut buf);
        }
        std::thread::sleep(Duration::from_millis(500));
    }
}

/// Launches the .NET API sidecar with the launch token and port injected via
/// environment variables. Returns the child handle for lifecycle management.
/// The sidecar binary is resolved from `src-tauri/binaries/HarborTorrent.Api-<target-triple>`.
fn start_api_sidecar(
    app: &tauri::AppHandle,
    launch_token: &str,
    api_port: u16,
    logs: Arc<Mutex<VecDeque<String>>>,
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
                    let msg = format!(
                        "[HarborTorrent] API sidecar started on http://127.0.0.1:{api_port}"
                    );
                    println!("{msg}");
                    push_sidecar_log(&logs, msg);

                    // Drain the receiver to keep the stdout/stderr pipes open.
                    // The launch token is intentionally not logged here.
                    tauri::async_runtime::spawn(async move {
                        while let Some(event) = rx.recv().await {
                            match event {
                                tauri_plugin_shell::process::CommandEvent::Stdout(line) => {
                                    let text = String::from_utf8_lossy(&line);
                                    print!("[HarborTorrent.Api] {text}");
                                    push_sidecar_log(&logs, format!("[stdout] {}", text.trim_end()));
                                }
                                tauri_plugin_shell::process::CommandEvent::Stderr(line) => {
                                    let text = String::from_utf8_lossy(&line);
                                    eprint!("[HarborTorrent.Api] {text}");
                                    push_sidecar_log(&logs, format!("[stderr] {}", text.trim_end()));
                                }
                                _ => {}
                            }
                        }
                    });

                    Some(child)
                }
                Err(e) => {
                    let msg = format!("[HarborTorrent] Failed to spawn API sidecar: {e}");
                    eprintln!("{msg}");
                    push_sidecar_log(&logs, msg);
                    None
                }
            }
        }
        Err(e) => {
            let msg = format!("[HarborTorrent] Failed to resolve API sidecar binary: {e}");
            eprintln!("{msg}");
            push_sidecar_log(&logs, msg);
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

/// Restarts the API sidecar process using the existing session token and port.
#[tauri::command]
fn restart_api_sidecar(
    app: tauri::AppHandle,
    session: tauri::State<AppSession>,
    sidecar: tauri::State<SidecarHandle>,
    logs: tauri::State<SidecarLogs>,
) -> Result<(), String> {
    if let Some(existing) = sidecar.0.lock().unwrap().take() {
        let _ = existing.kill();
    }

    match start_api_sidecar(&app, &session.launch_token, session.api_port, Arc::clone(&logs.0)) {
        Some(child) => {
            *sidecar.0.lock().unwrap() = Some(child);
            Ok(())
        }
        None => Err("Failed to restart the HarborTorrent background engine.".to_string()),
    }
}

/// Returns recent sidecar stdout/stderr diagnostic lines without exposing session secrets.
#[tauri::command]
fn get_sidecar_diagnostics(logs: tauri::State<SidecarLogs>) -> Vec<String> {
    logs.0
        .lock()
        .map(|guard| guard.iter().cloned().collect())
        .unwrap_or_default()
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let launch_token = generate_launch_token();
    let api_port = select_free_port();

    let session = AppSession {
        launch_token: launch_token.clone(),
        api_port,
    };

    let sidecar_logs = Arc::new(Mutex::new(VecDeque::with_capacity(MAX_SIDECAR_LOG_LINES)));

    tauri::Builder::default()
        .manage(session)
        .manage(SidecarHandle(Mutex::new(None)))
        .manage(SidecarLogs(Arc::clone(&sidecar_logs)))
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_process::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .plugin(tauri_plugin_notification::init())
        .plugin(tauri_plugin_fs::init())
        .invoke_handler(tauri::generate_handler![
            get_launch_token,
            get_api_port,
            restart_api_sidecar,
            get_sidecar_diagnostics
        ])
        .setup(move |app| {
            // Start the .NET backend and store the child handle for lifecycle management.
            let child = start_api_sidecar(&app.handle(), &launch_token, api_port, Arc::clone(&sidecar_logs));
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
            // Request graceful shutdown and kill the sidecar on exit so no orphaned process remains.
            if let RunEvent::Exit = event {
                let session = app.state::<AppSession>();
                let child_opt = app.state::<SidecarHandle>().0.lock().unwrap().take();
                if let Some(child) = child_opt {
                    request_graceful_shutdown(session.api_port, &session.launch_token);
                    let _ = child.kill();
                    println!("[HarborTorrent] API sidecar stopped.");
                }
            }
        });
}
