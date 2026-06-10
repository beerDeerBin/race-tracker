import { useState } from 'react';
import type { ChangeEvent, DragEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Star, Trash2, Upload } from 'lucide-react';
import { AuthImage } from './AuthImage';
import { ImagePreviewDialog } from './ImagePreviewDialog';
import { ConfirmDialog } from './ConfirmDialog';
import { useVehicleImages } from '../hooks/useVehicleImages';
import { useDeleteImage, useSetTitleImage, useUploadImage } from '../hooks/useImageMutations';
import { ALLOWED_IMAGE_TYPES, MAX_IMAGE_BYTES } from '../services/imageService';
import type { VehicleImageResponse, VehicleResponse } from '../models/api';

/**
 * The Gallery tab body: upload images (click or drag-and-drop, several at once), preview them
 * full-screen, delete them (with a confirmation), and choose one as the vehicle's title (avatar).
 * Pure UI over the image hooks — no fetching logic here. Keyboard-first: the dropzone wraps a
 * focusable file input, every thumbnail/action is a real button.
 */
export function VehicleGallery({ vehicle }: { vehicle: VehicleResponse }) {
    const { t } = useTranslation();
    const deviceGuid = vehicle.deviceGuid;

    const { data: images, isPending, isError } = useVehicleImages(deviceGuid);
    const upload = useUploadImage(deviceGuid);
    const remove = useDeleteImage(deviceGuid);
    const setTitle = useSetTitleImage(deviceGuid);

    const [previewId, setPreviewId] = useState<string | null>(null);
    const [deleting, setDeleting] = useState<VehicleImageResponse | null>(null);
    const [validationError, setValidationError] = useState<string | null>(null);
    const [dragging, setDragging] = useState(false);

    const handleFiles = (fileList: FileList | null) => {
        if (!fileList || fileList.length === 0) {
            return;
        }
        const valid: File[] = [];
        let error: string | null = null;
        for (const file of Array.from(fileList)) {
            if (!ALLOWED_IMAGE_TYPES.includes(file.type as (typeof ALLOWED_IMAGE_TYPES)[number])) {
                error = 'gallery.badType';
            } else if (file.size > MAX_IMAGE_BYTES) {
                error = 'gallery.tooLarge';
            } else {
                valid.push(file);
            }
        }
        setValidationError(error);
        if (valid.length > 0) {
            upload.mutate(valid);
        }
    };

    const onInputChange = (event: ChangeEvent<HTMLInputElement>) => {
        handleFiles(event.target.files);
        event.target.value = ''; // allow re-selecting the same file(s)
    };

    const onDrop = (event: DragEvent<HTMLLabelElement>) => {
        event.preventDefault();
        setDragging(false);
        handleFiles(event.dataTransfer.files);
    };

    const errorKey = validationError ?? (upload.isError ? 'gallery.uploadFailed' : null);

    const confirmDelete = () => {
        if (deleting) {
            remove.mutate(deleting.id, { onSettled: () => setDeleting(null) });
        }
    };

    return (
        <div>
            <label
                onDragOver={(event) => {
                    event.preventDefault();
                    setDragging(true);
                }}
                onDragLeave={() => setDragging(false)}
                onDrop={onDrop}
                className={`mb-4 flex cursor-pointer flex-col items-center justify-center gap-1 rounded-lg border-2 border-dashed px-6 py-8 text-center transition-colors focus-within:ring-2 focus-within:ring-f1-red ${
                    dragging
                        ? 'border-f1-red bg-f1-red/5'
                        : 'border-slate-300 hover:border-f1-red dark:border-slate-700'
                }`}
            >
                <Upload className="h-6 w-6 text-f1-red" aria-hidden="true" />
                <span className="text-sm font-medium text-slate-700 dark:text-slate-200">
                    {upload.isPending ? t('gallery.uploading') : t('gallery.dropzone')}
                </span>
                <span className="text-xs text-slate-500 dark:text-slate-400">
                    {t('gallery.uploadHint')}
                </span>
                <input
                    type="file"
                    accept={ALLOWED_IMAGE_TYPES.join(',')}
                    multiple
                    onChange={onInputChange}
                    disabled={upload.isPending}
                    className="sr-only"
                />
            </label>

            {errorKey && (
                <p role="alert" className="mb-4 text-sm text-red-600 dark:text-red-400">
                    {t(errorKey)}
                </p>
            )}

            {isPending && <p className="text-slate-500">{t('gallery.loading')}</p>}
            {isError && (
                <p role="alert" className="text-red-600 dark:text-red-400">
                    {t('gallery.loadFailed')}
                </p>
            )}

            {images &&
                (images.length === 0 ? (
                    <p className="text-slate-500 dark:text-slate-400">{t('gallery.empty')}</p>
                ) : (
                    <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
                        {images.map((image) => {
                            const isTitle = image.id === vehicle.titleImageId;
                            return (
                                <li key={image.id}>
                                    <button
                                        type="button"
                                        onClick={() => setPreviewId(image.id)}
                                        aria-label={t('gallery.openImage', {
                                            name: image.fileName,
                                        })}
                                        className="block aspect-square w-full overflow-hidden rounded-lg border border-slate-200 bg-slate-100 transition-shadow hover:shadow-md focus:ring-2 focus:ring-f1-red focus:outline-none dark:border-slate-700 dark:bg-slate-800"
                                    >
                                        <AuthImage
                                            deviceGuid={deviceGuid}
                                            imageId={image.id}
                                            alt={image.fileName}
                                            className="h-full w-full object-cover"
                                        />
                                    </button>
                                    <div className="mt-1 flex items-center justify-between gap-1">
                                        {isTitle ? (
                                            <span className="inline-flex items-center gap-1 rounded-full bg-f1-red/10 px-2 py-0.5 text-xs font-medium text-f1-red">
                                                <Star
                                                    className="h-3 w-3 fill-current"
                                                    aria-hidden="true"
                                                />
                                                {t('gallery.titleBadge')}
                                            </span>
                                        ) : (
                                            <span />
                                        )}
                                        <div className="flex items-center gap-1">
                                            <button
                                                type="button"
                                                onClick={() => setTitle.mutate(image.id)}
                                                disabled={isTitle || setTitle.isPending}
                                                aria-pressed={isTitle}
                                                aria-label={t('gallery.setTitle', {
                                                    name: image.fileName,
                                                })}
                                                title={t('gallery.setTitle', {
                                                    name: image.fileName,
                                                })}
                                                className="rounded p-1 text-slate-500 transition-colors hover:text-f1-red focus:ring-2 focus:ring-f1-red focus:outline-none disabled:cursor-not-allowed disabled:opacity-40 dark:text-slate-400"
                                            >
                                                <Star
                                                    className={`h-4 w-4 ${isTitle ? 'fill-current text-f1-red' : ''}`}
                                                    aria-hidden="true"
                                                />
                                            </button>
                                            <button
                                                type="button"
                                                onClick={() => setDeleting(image)}
                                                aria-label={t('gallery.delete', {
                                                    name: image.fileName,
                                                })}
                                                title={t('gallery.delete', {
                                                    name: image.fileName,
                                                })}
                                                className="rounded p-1 text-slate-500 transition-colors hover:text-red-600 focus:ring-2 focus:ring-red-500 focus:outline-none dark:text-slate-400"
                                            >
                                                <Trash2 className="h-4 w-4" aria-hidden="true" />
                                            </button>
                                        </div>
                                    </div>
                                </li>
                            );
                        })}
                    </ul>
                ))}

            {previewId && (
                <ImagePreviewDialog
                    deviceGuid={deviceGuid}
                    imageId={previewId}
                    alt={t('vehicles.avatarAlt', { name: vehicle.name })}
                    onClose={() => setPreviewId(null)}
                />
            )}

            {deleting && (
                <ConfirmDialog
                    title={t('gallery.deleteTitle')}
                    message={t('gallery.deleteConfirm', { name: deleting.fileName })}
                    confirmLabel={t('gallery.deleteConfirmButton')}
                    danger
                    busy={remove.isPending}
                    onConfirm={confirmDelete}
                    onClose={() => setDeleting(null)}
                />
            )}
        </div>
    );
}
