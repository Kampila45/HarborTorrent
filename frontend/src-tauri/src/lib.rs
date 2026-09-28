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
                Ok((mut rx, _child)) => {
                    println!("[HarborTorrent] API sidecar started on http://127.0.0.1:5000");
                    
                    // Drain the receiver to keep the stdout pipe open.
                    tauri::async_runtime::spawn(async move {
                        while let Some(_) = rx.recv().await {}
                    });
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

use tauri::{
    menu::{Menu, MenuItem},
    tray::TrayIconBuilder,
    Manager, WindowEvent,
};

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_process::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .plugin(tauri_plugin_notification::init())
        .plugin(tauri_plugin_fs::init())
        .setup(|app| {
            // Start the .NET backend when the Tauri window opens.
            start_api_sidecar(&app.handle());

            // Setup Tray Menu
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
                    } = event {
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
        .on_window_event(|window, event| match event {
            WindowEvent::CloseRequested { api, .. } => {
                window.hide().unwrap();
                api.prevent_close();
            }
            _ => {}
        })
        .run(tauri::generate_context!())
        .expect("error while running HarborTorrent");
}
