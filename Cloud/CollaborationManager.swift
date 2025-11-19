// File: Cloud/CollaborationManager.swift
// CollaborationManager for CloudKit sharing on iOS 16+ / Swift 6
// Fixed: No usage of CKShare.rootRecord or CKShare.recordIDs (unsupported APIs)

import CloudKit
import SwiftUI
import os.log

private let log = Logger(subsystem: "com.cardgamebuilder.cloud", category: "Collaboration")

@MainActor
final class CollaborationManager: ObservableObject {

    // MARK: - Published Properties

    @Published var activeShares: [String: CKShare] = [:]  // Empty dictionary literal
    @Published var pendingInvitations: [CKShare.Metadata] = []
    @Published var sharingState: [String: SharingState] = [:]  // Empty dictionary literal
    @Published var errorMessage: String?

    // MARK: - Private Properties

    private let container: CKContainer
    private let database: CKDatabase

    // MARK: - Initialization

    init(container: CKContainer = .default()) {
        self.container = container
        self.database = container.sharedCloudDatabase
    }

    // MARK: - Sharing State

    enum SharingState {
        case notShared
        case shared
        case pending
        case error(String)
    }

    // MARK: - Create Share

    /// Creates a CKShare for a project record using CKModifyRecordsOperation
    func createShare(for projectRecord: CKRecord, projectID: String) async throws -> CKShare {
        log.debug("Creating share for project: \(projectID)")

        let share = CKShare(rootRecord: projectRecord)
        share[CKShare.SystemFieldKey.title] = "Card Game Project" as CKRecordValue
        share.publicPermission = .none

        return try await withCheckedThrowingContinuation { continuation in
            let operation = CKModifyRecordsOperation(
                recordsToSave: [projectRecord, share],
                recordIDsToDelete: nil
            )

            operation.savePolicy = .allKeys
            operation.qualityOfService = .userInitiated

            operation.modifyRecordsResultBlock = { result in
                switch result {
                case .success:
                    Task { @MainActor in
                        self.activeShares[projectID] = share
                        self.sharingState[projectID] = .shared
                    }
                    continuation.resume(returning: share)

                case .failure(let error):
                    log.error("Failed to create share: \(error.localizedDescription)")
                    Task { @MainActor in
                        self.sharingState[projectID] = .error(error.localizedDescription)
                        self.errorMessage = error.localizedDescription
                    }
                    continuation.resume(throwing: error)

                @unknown default:
                    let unknownError = NSError(
                        domain: "CollaborationManager",
                        code: -1,
                        userInfo: [NSLocalizedDescriptionKey: "Unknown result type"]
                    )
                    continuation.resume(throwing: unknownError)
                }
            }

            database.add(operation)
        }
    }

    // MARK: - Accept Share

    /// Accepts a share invitation using metadata
    func acceptShare(metadata: CKShare.Metadata) async throws {
        // FIXED: Use metadata.rootRecordID for root identification (not share.rootRecord)
        let rootRecordName = metadata.rootRecordID.recordName  // OK to use for logging
        log.debug("Accepting share: \(rootRecordName)")

        sharingState[rootRecordName] = .pending

        do {
            let share = try await container.accept(metadata)

            // FIXED: Use share.recordID for share identification (not share.rootRecord)
            let acceptedShareID = share.recordID.recordName  // share's own record id
            log.info("Accepted share (shareID): \(acceptedShareID)")

            // If you need the root record id post-accept, reuse metadata.rootRecordID
            let rootID = metadata.rootRecordID.recordName

            activeShares[rootID] = share
            sharingState[rootID] = .shared

            // Remove from pending invitations
            pendingInvitations.removeAll { $0.share.recordID == metadata.share.recordID }

        } catch {
            log.error("Failed to accept share: \(error.localizedDescription)")
            sharingState[rootRecordName] = .error(error.localizedDescription)
            errorMessage = error.localizedDescription
            throw error
        }
    }

    // MARK: - Fetch Pending Invitations

    func fetchPendingInvitations() async throws {
        log.debug("Fetching pending share invitations")

        let metadataArray = try await container.fetchAllSharedRecordMetadata()

        pendingInvitations = metadataArray.filter { metadata in
            metadata.participantStatus == .pending
        }

        log.info("Found \(self.pendingInvitations.count) pending invitations")
    }

    // MARK: - Remove Share

    func removeShare(for projectID: String) async throws {
        log.debug("Removing share for project: \(projectID)")

        guard let share = activeShares[projectID] else {
            log.warning("No active share found for project: \(projectID)")
            return
        }

        // FIXED: Use share.recordID for deletion (not share.rootRecord)
        let shareRecordID = share.recordID  // Use share's own record ID

        try await database.deleteRecord(withID: shareRecordID)

        activeShares.removeValue(forKey: projectID)
        sharingState[projectID] = .notShared

        log.info("Removed share: \(shareRecordID.recordName)")
    }

    // MARK: - Update Participant Permissions

    func updateParticipantPermission(
        share: CKShare,
        participant: CKShare.Participant,
        permission: CKShare.ParticipantPermission
    ) async throws {
        log.debug("Updating participant permission")

        participant.permission = permission

        try await database.save(share)

        log.info("Updated participant permission to: \(permission.rawValue)")
    }

    // MARK: - Remove Participant

    func removeParticipant(
        share: CKShare,
        participant: CKShare.Participant,
        projectID: String
    ) async throws {
        log.debug("Removing participant from share")

        share.removeParticipant(participant)

        let updatedShare = try await database.save(share)
        activeShares[projectID] = updatedShare

        log.info("Removed participant from share")
    }

    // MARK: - Fetch Share

    func fetchShare(for projectID: String, recordID: CKRecord.ID) async throws -> CKShare? {
        log.debug("Fetching share for project: \(projectID)")

        do {
            let record = try await database.record(for: recordID)

            if let share = record as? CKShare {
                activeShares[projectID] = share
                sharingState[projectID] = .shared
                return share
            } else if let shareReference = record.share {
                let shareRecord = try await database.record(for: shareReference.recordID)
                if let share = shareRecord as? CKShare {
                    activeShares[projectID] = share
                    sharingState[projectID] = .shared
                    return share
                }
            }

            return nil

        } catch let error as CKError {
            switch error.code {
            case .unknownItem:
                log.info("No share found for project: \(projectID)")
                sharingState[projectID] = .notShared
                return nil

            default:
                log.error("Error fetching share: \(error.localizedDescription)")
                sharingState[projectID] = .error(error.localizedDescription)
                throw error

            @unknown default:
                log.error("Unknown CKError: \(error.localizedDescription)")
                throw error
            }
        }
    }
}

// MARK: - SwiftUI Sharing Wrapper

extension CollaborationManager {

    /// Presents the system CloudKit sharing UI
    @MainActor
    func presentSharing(for share: CKShare, container: CKContainer) -> UICloudSharingController? {
        let controller = UICloudSharingController(share: share, container: container)
        return controller
    }
}

// MARK: - SwiftUI View Modifier

struct CloudSharingView: UIViewControllerRepresentable {
    let share: CKShare
    let container: CKContainer
    let onDismiss: () -> Void

    func makeUIViewController(context: Context) -> UICloudSharingController {
        let controller = UICloudSharingController(share: share, container: container)
        controller.delegate = context.coordinator
        return controller
    }

    func updateUIViewController(_ uiViewController: UICloudSharingController, context: Context) {
        // No updates needed
    }

    func makeCoordinator() -> Coordinator {
        Coordinator(onDismiss: onDismiss)
    }

    class Coordinator: NSObject, UICloudSharingControllerDelegate {
        let onDismiss: () -> Void

        init(onDismiss: @escaping () -> Void) {
            self.onDismiss = onDismiss
        }

        func cloudSharingController(
            _ csc: UICloudSharingController,
            failedToSaveShareWithError error: Error
        ) {
            log.error("Failed to save share: \(error.localizedDescription)")
            onDismiss()
        }

        func itemTitle(for csc: UICloudSharingController) -> String? {
            "Card Game Project"
        }

        func cloudSharingControllerDidSaveShare(_ csc: UICloudSharingController) {
            log.info("Share saved successfully")
            onDismiss()
        }

        func cloudSharingControllerDidStopSharing(_ csc: UICloudSharingController) {
            log.info("Stopped sharing")
            onDismiss()
        }
    }
}
