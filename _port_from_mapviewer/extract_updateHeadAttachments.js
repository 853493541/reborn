  updateHeadAttachments() {
    const attachments = this.current?.attachments;
    if (!attachments) return;

    for (const attachment of attachments.rootLinks || []) {
      const parent = attachment.rootBone.parent;
      attachment.targetWorld.multiplyMatrices(attachment.anchorBone.matrixWorld, attachment.localOffset);

      if (parent) {
        attachment.parentInverse.copy(parent.matrixWorld).invert();
        attachment.localMatrix.multiplyMatrices(attachment.parentInverse, attachment.targetWorld);
      } else {
        attachment.localMatrix.copy(attachment.targetWorld);
      }

      attachment.localMatrix.decompose(
        attachment.position,
        attachment.quaternion,
        attachment.scale,
      );

      attachment.rootBone.position.copy(attachment.position);
      attachment.rootBone.quaternion.copy(attachment.quaternion);
      attachment.rootBone.scale.copy(attachment.scale);
    }

    if (this.current?.root) {
      this.current.root.updateMatrixWorld(true);
    }

    for (const attachment of attachments.boneLinks || []) {
      const parent = attachment.targetBone.parent;
      attachment.targetWorld.copy(attachment.sourceBone.matrixWorld);

      if (parent) {
        attachment.parentInverse.copy(parent.matrixWorld).invert();
        attachment.localMatrix.multiplyMatrices(attachment.parentInverse, attachment.targetWorld);
      } else {
        attachment.localMatrix.copy(attachment.targetWorld);
      }

      attachment.localMatrix.decompose(
        attachment.position,
        attachment.quaternion,
        attachment.scale,
      );

      attachment.targetBone.position.copy(attachment.position);
      attachment.targetBone.quaternion.copy(attachment.quaternion);
      attachment.targetBone.scale.copy(attachment.scale);
    }
  }